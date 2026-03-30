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

    app.UseWedaCore<AppDbContext>(options =>
    {
        options.EnsureDatabaseCreated = false; // Already done above
        options.SwaggerEndpointUrl = "/swagger/v1/swagger.json";
        options.SwaggerEndpointName = "OpenClaw.NET API V1";
        options.RoutePrefix = "swagger";
    });

    app.Run();
}