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
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Infrastructure;
using OpenClaw.Infrastructure.Common.Persistence;
using OpenClaw.Hosting;


var builder = WebApplication.CreateBuilder(args);
{
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    builder.Services
        .AddOpenClaw(builder.Configuration)
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
                    Title = "Weda API",
                    Version = "v1",
                };
                options.Observability.ServiceName = "WedaTemplate";
                options.Observability.Tracing.UseConsoleExporter = true;
            },
            nats =>
            {
                nats.DefaultConnection = "bus";
                nats.AddConnection(EcoType.Protobuf ,"broker", "nats://localhost:4222");
                nats.AddConnection(EcoType.Json, "bus", "nats://localhost:4223");

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

        // Try to create database, if schema mismatch occurs, recreate
        try
        {
            dbContext.Database.EnsureCreated();

            // Verify schema by testing a simple query on each DbSet
            _ = await dbContext.Set<User>().AnyAsync();
        }
        catch (Exception ex) when (ex.Message.Contains("no such table") || ex.Message.Contains("doesn't exist"))
        {
            logger.LogWarning("Database schema mismatch detected, recreating database...");
            dbContext.Database.EnsureDeleted();
            dbContext.Database.EnsureCreated();
        }

        var seeder = scope.ServiceProvider.GetRequiredService<AppDbContextSeeder>();
        await seeder.SeedAsync();
    }

    app.UseWedaCore<AppDbContext>(options =>
    {
        options.EnsureDatabaseCreated = false; // Already done above
        options.SwaggerEndpointUrl = "/swagger/v1/swagger.json";
        options.SwaggerEndpointName = "Weda API V1";
        options.RoutePrefix = "swagger";
    });

    app.Run();
}