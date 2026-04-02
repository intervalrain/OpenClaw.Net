using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Weda.Core;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Core.Infrastructure.Messaging.Nats.Middleware;
using Weda.Protocol.Enums;
using Weda.Protocol;
using OpenClaw.Api;
using OpenClaw.Application;
using OpenClaw.Contracts;
using OpenClaw.Infrastructure;
using OpenClaw.Infrastructure.Common.Persistence;
using OpenClaw.Hosting;
using OpenClaw.Hosting.Observability;
using OpenClaw.Api.Security;
using OpenClaw.Application.CronJobs;


var builder = WebApplication.CreateBuilder(args);
{
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Security services
    builder.Services.AddSingleton<LoginRateLimiter>();
    builder.Services.AddSingleton<RegistrationRateLimiter>();

    // CORS — restrict to configured origins
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? (builder.Environment.IsDevelopment()
                    ? ["http://localhost:5000", "https://localhost:5001"]
                    : []);

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services
        .AddOpenClaw(builder.Configuration)
        .AddOpenClawTelemetry(builder.Configuration)
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
        // Background services
        .AddHostedService<CronJobSchedulerService>()
        .AddHostedService<OpenClaw.Api.Audit.AuditLogCleanupService>()
        .AddWedaCore<IAssemblyMarker, IContractsMarker, IApplicationMarker>(
            builder.Configuration,
            services => services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.Assemblies = [typeof(IApplicationMarker).Assembly];
            }),
            options =>
            {
                options.XmlCommentAssemblies = [Assembly.GetExecutingAssembly()];
                options.OpenApiInfo = new OpenApiInfo
                {
                    Title = "OpenClaw.NET API",
                    Version = "v1",
                };
                options.Observability.ServiceName = "OpenClaw.NET";
                options.Observability.Tracing.UseConsoleExporter = false;
            },
            nats =>
            {
                nats.DefaultConnection = "bus";
                var brokerUrl = builder.Configuration["Nats:Broker:Url"] ?? "nats://localhost:4222";
                var busUrl = builder.Configuration["Nats:Bus:Url"] ?? "nats://localhost:4223";
                nats.AddConnection(EcoType.Protobuf, "broker", brokerUrl);
                nats.AddConnection(EcoType.Json, "bus", busUrl);

                nats.AddKeyValueCache();
                nats.AddObjectStore();
                nats.Use<Weda.Core.Infrastructure.Messaging.Nats.Middleware.AuditLoggingMiddleware>();
            }
        );
}

var app = builder.Build();
{
    // Security headers (CSP, X-Frame-Options, HSTS, etc.)
    app.UseMiddleware<SecurityHeadersMiddleware>();

    // CORS
    app.UseCors();

    // Enable static files (UseDefaultFiles must come before UseStaticFiles)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Auto-migrate database and seed
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        // Detect corrupted state: __EFMigrationsHistory says InitialCreate ran but tables are missing.
        // This happens when a legacy DB (created via EnsureCreated) was incorrectly baselined.
        // Fix: drop everything and let Migrate() rebuild from scratch.
        var conn = dbContext.Database.GetDbConnection();
        await conn.OpenAsync();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_name = '__EFMigrationsHistory'
                )
            """;
            var historyExists = (bool)(await cmd.ExecuteScalarAsync())!;

            if (historyExists)
            {
                // Check if a required table from InitialCreate is actually present
                cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'cron_jobs')";
                var schemaValid = (bool)(await cmd.ExecuteScalarAsync())!;

                if (!schemaValid)
                {
                    logger.LogWarning("Corrupted migration state detected (history exists but schema incomplete). Rebuilding database...");
                    await conn.CloseAsync();
                    dbContext.Database.EnsureDeleted();
                    dbContext.Database.EnsureCreated();
                    await conn.OpenAsync();

                    // Seed __EFMigrationsHistory so future Migrate() calls work correctly
                    using var seedCmd = conn.CreateCommand();
                    seedCmd.CommandText = """
                        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                            "MigrationId" varchar(150) NOT NULL PRIMARY KEY,
                            "ProductVersion" varchar(32) NOT NULL
                        );
                    """;
                    await seedCmd.ExecuteNonQueryAsync();

                    foreach (var migration in dbContext.Database.GetMigrations())
                    {
                        seedCmd.CommandText = $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{migration}', '9.0.0') ON CONFLICT DO NOTHING";
                        await seedCmd.ExecuteNonQueryAsync();
                    }
                    logger.LogInformation("Database rebuilt and migration history seeded");
                }
            }
        }

        var pending = dbContext.Database.GetPendingMigrations().ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));
        }
        dbContext.Database.Migrate();
        logger.LogInformation("Database migration completed");

        var seeder = scope.ServiceProvider.GetRequiredService<AppDbContextSeeder>();
        await seeder.SeedAsync();
    }

    // Ensure workspace directories exist
    var workspaceBasePath = Environment.GetEnvironmentVariable("OPENCLAW_WORKSPACE_PATH")
        ?? Path.Combine(AppContext.BaseDirectory, "workspace");
    Directory.CreateDirectory(workspaceBasePath);
    app.Logger.LogInformation("Workspace base path: {WorkspacePath}", workspaceBasePath);

    app.UseWedaCore<AppDbContext>(options =>
    {
        options.EnsureDatabaseCreated = false;
        options.SwaggerEndpointUrl = "/swagger/v1/swagger.json";
        options.SwaggerEndpointName = "OpenClaw.NET API V1";
        options.RoutePrefix = "swagger";
    });

    // Audit logging (after auth so context.User is populated)
    app.UseMiddleware<OpenClaw.Api.Security.AuditLoggingMiddleware>();

    // Real-time ban enforcement (after auth, checks DB on every authenticated request)
    app.UseMiddleware<OpenClaw.Api.Security.BanCheckMiddleware>();

    app.Run();
}