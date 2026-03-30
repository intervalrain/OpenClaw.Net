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
        // Background scheduler services
        .AddHostedService<CronJobSchedulerService>()
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

    // Audit logging for security-critical operations
    app.UseMiddleware<OpenClaw.Api.Security.AuditLoggingMiddleware>();

    // Enable static files (UseDefaultFiles must come before UseStaticFiles)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // Auto-migrate database and seed
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            // Detect legacy databases created with EnsureCreated (no migration history table).
            // If tables exist but __EFMigrationsHistory doesn't, baseline all existing migrations
            // so Migrate() won't try to re-create existing schema.
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '__EFMigrationsHistory')";
                var historyExists = (bool)(await cmd.ExecuteScalarAsync())!;

                if (!historyExists)
                {
                    // Check if this is an existing database (has our tables) or a fresh one
                    cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users')";
                    var isExistingDb = (bool)(await cmd.ExecuteScalarAsync())!;

                    if (isExistingDb)
                    {
                        logger.LogInformation("Legacy database detected (created with EnsureCreated). Baselining migration history...");
                        cmd.CommandText = """
                            CREATE TABLE "__EFMigrationsHistory" (
                                "MigrationId" varchar(150) NOT NULL PRIMARY KEY,
                                "ProductVersion" varchar(32) NOT NULL
                            );
                        """;
                        await cmd.ExecuteNonQueryAsync();

                        // Mark all known migrations as applied
                        var applied = dbContext.Database.GetMigrations();
                        foreach (var migration in applied)
                        {
                            cmd.CommandText = $"""INSERT INTO "__EFMigrationsHistory" VALUES ('{migration}', '10.0.0')""";
                            await cmd.ExecuteNonQueryAsync();
                        }
                        logger.LogInformation("Baselined {Count} migration(s)", applied.Count());
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed");
            throw;
        }

        var seeder = scope.ServiceProvider.GetRequiredService<AppDbContextSeeder>();
        await seeder.SeedAsync();
    }

    app.UseWedaCore<AppDbContext>(options =>
    {
        options.EnsureDatabaseCreated = false;
        options.SwaggerEndpointUrl = "/swagger/v1/swagger.json";
        options.SwaggerEndpointName = "OpenClaw.NET API V1";
        options.RoutePrefix = "swagger";
    });

    // Real-time ban enforcement (after auth, checks DB on every authenticated request)
    app.UseMiddleware<OpenClaw.Api.Security.BanCheckMiddleware>();

    app.Run();
}