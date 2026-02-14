using Asp.Versioning;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using Weda.Core.Application.Behaviors;
using Weda.Core.Infrastructure.Middleware;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Core.Presentation.Swagger;
using Weda.Core.Presentation.Filters;
using Weda.Core.Infrastructure.Observability;

namespace Weda.Core;

public static class WedaCoreModule
{
    public static IServiceCollection AddWedaCore<TAssemblyMarker, TContractsMarker, TApplicationMarker>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceCollection> configureMediator,
        Action<WedaCoreOptions>? configure = null,
        Action<NatsBuilder>? configureNats = null)
    {
        var options = new WedaCoreOptions();
        configure?.Invoke(options);

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        configureMediator(services);
        services.AddObservability(options.Observability);

        if (options.Messaging.Enabled)
        {
            services.AddMessaging<TAssemblyMarker>(configuration, configureNats);
        }

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
        services.AddValidatorsFromAssemblyContaining<TApplicationMarker>();

        services.AddPresentation<TContractsMarker>(options);

        return services;
    }

    public static void AddWedaCoreSwagger(this SwaggerGenOptions options, OpenApiInfo info)
    {
        options.SwaggerDoc(info.Version, info);

        options.SchemaFilter<ResponseExampleSchemaFilter>();

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme.\n\n" +
                          "Enter your token in the text input below.\n\n" +
                          "Example: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
        });

        options.OperationFilter<AuthorizeCheckOperationFilter>();
    }

    public static WebApplication UseWedaCore<TDbContext>(
        this WebApplication app,
        Action<WedaCoreAppOptions>? configure = null)
        where TDbContext : DbContext
    {
        var options = new WedaCoreAppOptions();
        configure?.Invoke(options);

        if (options.EnsureDatabaseCreated)
        {
            app.EnsureDatabaseCreated<TDbContext>();
        }

        app.UseInfrastructure<TDbContext>();
        app.UseExceptionHandler();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(uiOptions =>
            {
                uiOptions.SwaggerEndpoint(options.SwaggerEndpointUrl, options.SwaggerEndpointName);
                uiOptions.RoutePrefix = options.RoutePrefix;

                options.ConfigureSwaggerUI?.Invoke(uiOptions);
            });
        }
        else
        {
            // Only use HTTPS redirection in non-development environments
            app.UseHttpsRedirection();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }

    private static IApplicationBuilder EnsureDatabaseCreated<TDbContext>(this IApplicationBuilder app)
        where TDbContext : DbContext
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        dbContext.Database.EnsureCreated();
        return app;
    }

    private static IServiceCollection AddPresentation<TExamplesMarker>(
        this IServiceCollection services,
        WedaCoreOptions options)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = WedaJsonDefaults.Options.PropertyNamingPolicy;
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = WedaJsonDefaults.Options.PropertyNameCaseInsensitive;
            });
        services.AddEndpointsApiExplorer();

        services.AddApiVersioning(versioningOptions =>
        {
            versioningOptions.DefaultApiVersion = options.DefaultApiVersion;
            versioningOptions.AssumeDefaultVersionWhenUnspecified = true;
            versioningOptions.ReportApiVersions = true;
            versioningOptions.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(explorerOptions =>
        {
            explorerOptions.GroupNameFormat = options.ApiVersionGroupNameFormat;
            explorerOptions.SubstituteApiVersionInUrl = true;
        });

        services.AddSwaggerExamplesFromAssemblyOf<TExamplesMarker>();

        services.AddSwaggerGen(swaggerOptions =>
        {
            foreach (var assembly in options.XmlCommentAssemblies)
            {
                var xmlFilename = $"{assembly.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
                if (File.Exists(xmlPath))
                {
                    swaggerOptions.IncludeXmlComments(xmlPath);
                }
            }

            swaggerOptions.ExampleFilters();
            swaggerOptions.AddWedaCoreSwagger(options.OpenApiInfo);

            options.ConfigureSwagger?.Invoke(swaggerOptions);
        });

        return services;
    }

    private static IServiceCollection AddMessaging<TAssemblyMarker>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<NatsBuilder>? configureNats = null)
    {
        var natsOptions = new NatsOptions();
        configuration.GetSection(NatsOptions.SectionName).Bind(natsOptions);
        services.AddNats(builder =>
        {
            builder.BindConfiguration(natsOptions);
            configureNats?.Invoke(builder);
        });

        // Register EventControllers from the API assembly (where controllers are defined)
        services.AddEventControllers(typeof(TAssemblyMarker).Assembly);

        return services;
    }

    private static IApplicationBuilder UseInfrastructure<TDbContext>(this IApplicationBuilder app)
        where TDbContext : DbContext
    {
        app.UseMiddleware<EventualConsistencyMiddleware<TDbContext>>();

        return app;
    }
}
