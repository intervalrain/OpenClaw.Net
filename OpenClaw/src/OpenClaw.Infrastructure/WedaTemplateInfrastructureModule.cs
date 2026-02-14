using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using OpenClaw.Domain.Users.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using OpenClaw.Infrastructure.Persistence;
using OpenClaw.Infrastructure.Security;
using OpenClaw.Infrastructure.Security.PolicyEnforcer;
using OpenClaw.Infrastructure.Security.TokenValidation;
using OpenClaw.Infrastructure.Services;
using OpenClaw.Infrastructure.Users.Persistence;

using InfraCurrentUserProvider = OpenClaw.Infrastructure.Security.CurrentUserProvider.CurrentUserProvider;
using InfraPasswordHasher = OpenClaw.Infrastructure.Security.PasswordHasher.BCryptPasswordHasher;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Weda.Core.Application.Security.Models;




namespace OpenClaw.Infrastructure;

public static class WedaTemplateInfrastructureModule
{
    private const string DatabaseSection = "Database";
    private const string AuthenticationSection = "Authentication";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseOptions = configuration.GetSection(DatabaseSection).Get<DatabaseOptions>() ?? new DatabaseOptions();
        var authOptions = configuration.GetSection(AuthenticationSection).Get<AuthenticationOptions>() ?? new AuthenticationOptions();

        services
            .Configure<DatabaseOptions>(configuration.GetSection(DatabaseSection))
            .Configure<AuthenticationOptions>(configuration.GetSection(AuthenticationSection))
            .AddHttpContextAccessor()
            .AddServices()
            .AddPersistence(databaseOptions);

        if (authOptions.Enabled)
        {
            services
                .AddJwtAuthentication(configuration)
                .AddAuthorizationPolicies();
        }

        return services;
    }

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, DatabaseOptions options)
    {
        services.AddDbContext<AppDbContext>(dbOptions =>
        {
            dbOptions.UseNpgsql(options.ConnectionString);
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());


        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, InfraPasswordHasher>();
        services.AddScoped<AppDbContextSeeder>();

        return services;
    }

    private static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<ICurrentUserProvider, InfraCurrentUserProvider>();
        services.AddSingleton<IPolicyEnforcer, PolicyEnforcer>();

        services.AddAuthorizationBuilder()
            .AddPolicy(Policy.AdminOrAbove, policy =>
                policy.RequireRole(Role.Admin, Role.SuperAdmin))
            .AddPolicy(Policy.SuperAdminOnly, policy =>
                policy.RequireRole(Role.SuperAdmin));

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.Section));
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services
            .ConfigureOptions<JwtBearerTokenValidationConfiguration>()
            .AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        return services;
    }
}
