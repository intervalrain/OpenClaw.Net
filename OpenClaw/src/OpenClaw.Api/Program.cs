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


var builder = WebApplication.CreateBuilder(args);
{
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    builder.Services
        .AddOpenClaw(builder.Configuration)
        .AddOpenClawTelemetry(builder.Configuration)
        .AddApplication()
        .AddInfrastructure(builder.Configuration)
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
                nats.Use<AuditLoggingMiddleware>();
            }
        );
}

var app = builder.Build();
{
    // Enable static files for organization chart UI
    app.UseStaticFiles();
    app.UseDefaultFiles();

    // Ensure database and seed in development
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            // Try EnsureCreated first (idempotent if schema exists)
            var created = dbContext.Database.EnsureCreated();
            if (created)
                logger.LogInformation("Database created");
            else
                logger.LogInformation("Database already exists");
        }
        catch (Exception ex)
        {
            // Schema mismatch or corruption, recreate
            logger.LogWarning(ex, "Database schema issue, recreating...");
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
            logger.LogInformation("Database recreated successfully");
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