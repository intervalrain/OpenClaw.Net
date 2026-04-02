using System.Reflection;
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
using OpenClaw.Api.Audit;
using Weda.Core.Infrastructure.Messaging.Nats.Locking;


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
        .AddHostedService<AuditLogCleanupService>()
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
                nats.Use<NatsAuditLoggingMiddleware>();

                // Register distributed lock for scheduler leader election
                _ = builder.Services.AddSingleton(sp =>
                {
                    var connProvider = sp.GetRequiredService<INatsConnectionProvider>();
                    var lockLogger = sp.GetRequiredService<ILogger<NatsKvDistributedLock>>();
                    return new NatsKvDistributedLock(connProvider, lockLogger, "bus");
                });
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
    await DatabaseMigrator.MigrateAndSeedAsync(app.Services);

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
    app.UseMiddleware<AuditLoggingMiddleware>();

    // Real-time ban enforcement (after auth, checks DB on every authenticated request)
    app.UseMiddleware<BanCheckMiddleware>();

    app.Run();
}