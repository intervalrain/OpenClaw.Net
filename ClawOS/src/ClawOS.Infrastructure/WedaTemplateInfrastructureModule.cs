using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using ClawOS.Contracts.Configuration;
using ClawOS.Contracts.Security;
using ClawOS.Domain.Audit.Repositories;
using ClawOS.Domain.Chat.Repositories;
using ClawOS.Domain.Configuration.Repositories;
using ClawOS.Domain.Users.Repositories;
using ClawOS.Domain.Skills.Repositories;
using ClawOS.Infrastructure.Common.Persistence;
using ClawOS.Infrastructure.Persistence;
using ClawOS.Infrastructure.Security;
using ClawOS.Infrastructure.Security.PolicyEnforcer;
using ClawOS.Infrastructure.Security.TokenValidation;
using ClawOS.Infrastructure.Services;
using ClawOS.Infrastructure.Users.Persistence;
using ClawOS.Infrastructure.Chat.Persistence;
using ClawOS.Infrastructure.Audit.Persistence;
using ClawOS.Infrastructure.Configuration.Persistence;
using ClawOS.Infrastructure.Security.CurrentUserProvider;
using ClawOS.Infrastructure.Security.PasswordHasher;
using ClawOS.Infrastructure.Skills.Persistence;
using ClawOS.Infrastructure.Configuration;

namespace ClawOS.Infrastructure;

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

        // persistence
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IModelProviderRepository, ModelProviderRepository>();
        services.AddScoped<IUserModelProviderRepository, UserModelProviderRepository>();
        services.AddScoped<IChannelSettingsRepository, ChannelSettingsRepository>();
        services.AddScoped<ISkillSettingRepository, SkillSettingRepository>();
        services.AddScoped<IAppConfigRepository, AppConfigRepository>();
        services.AddScoped<IUserPreferenceRepository, UserPreferenceRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // configuration (chain: Database -> Environment)
        // EnvironmentConfigStore is the terminal store (no fallback, read-only for env vars/.env file)
        services.AddSingleton(new EnvironmentConfigStore());
        services.AddScoped<IConfigStore>(sp =>
        {
            var envStore = sp.GetRequiredService<EnvironmentConfigStore>();
            var repository = sp.GetRequiredService<IAppConfigRepository>();
            var encryption = sp.GetRequiredService<IEncryptionService>();
            var uow = sp.GetRequiredService<IUnitOfWork>();

            return new DatabaseConfigStore(repository, encryption, uow, fallback: envStore);
        });

        // user configuration (per-user, encrypted)
        services.AddScoped<IUserConfigRepository, UserConfigRepository>();
        services.AddScoped<IUserConfigStore>(sp =>
        {
            var repository = sp.GetRequiredService<IUserConfigRepository>();
            var encryption = sp.GetRequiredService<IEncryptionService>();
            var uow = sp.GetRequiredService<IUnitOfWork>();
            return new DatabaseUserConfigStore(repository, encryption, uow);
        });

        // security
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<AppDbContextSeeder>();

        return services;
    }

    private static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
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
        var jwtSection = configuration.GetSection(JwtSettings.Section);
        var secret = jwtSection["Secret"];

        // Validate JWT secret at startup — reject placeholder or weak secrets
        if (string.IsNullOrWhiteSpace(secret)
            || secret.StartsWith("${")
            || secret.Contains("super-secret")
            || secret.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret is not configured or is too weak. " +
                "Set a cryptographically strong secret (>=32 chars) via environment variable 'JwtSettings__Secret' " +
                "or the JwtSettings:Secret configuration path.");
        }

        services.Configure<JwtSettings>(jwtSection);
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services
            .ConfigureOptions<JwtBearerTokenValidationConfiguration>()
            .AddAuthentication(defaultScheme: JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        return services;
    }

}
