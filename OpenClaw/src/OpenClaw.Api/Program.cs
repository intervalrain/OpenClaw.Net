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
            // Detect databases where schema exists but migration history is missing or incomplete.
            // This can happen with EnsureCreated, partial migrations, or volume reuse across deploys.
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using (var cmd = conn.CreateCommand())
            {
                // Check if our application tables exist (i.e. schema is already in place)
                cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'app_configs')";
                var schemaExists = (bool)(await cmd.ExecuteScalarAsync())!;

                if (schemaExists)
                {
                    // Ensure __EFMigrationsHistory table exists
                    cmd.CommandText = """
                        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                            "MigrationId" varchar(150) NOT NULL PRIMARY KEY,
                            "ProductVersion" varchar(32) NOT NULL
                        );
                    """;
                    await cmd.ExecuteNonQueryAsync();

                    // Baseline any migrations whose tables already exist in the database
                    var allMigrations = dbContext.Database.GetMigrations().ToList();
                    var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToHashSet();
                    var missing = allMigrations.Except(appliedMigrations).ToList();

                    if (missing.Count > 0)
                    {
                        // Only baseline the InitialCreate migration (which creates all base tables).
                        // Later migrations should run normally to apply incremental schema changes.
                        var initialMigration = missing.FirstOrDefault(m => m.Contains("InitialCreate"));
                        if (initialMigration != null)
                        {
                            cmd.CommandText = $"""INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('{initialMigration}', '10.0.0') ON CONFLICT DO NOTHING""";
                            await cmd.ExecuteNonQueryAsync();
                            logger.LogInformation("Baselined InitialCreate migration: {Migration}", initialMigration);
                        }
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
        options.EnsureDatabaseCreated = false; // Already done above
        options.SwaggerEndpointUrl = "/swagger/v1/swagger.json";
        options.SwaggerEndpointName = "OpenClaw.NET API V1";
        options.RoutePrefix = "swagger";
    });

    app.Run();
}