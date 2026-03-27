using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Application.Pipelines;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application;

public static class WedaTemplateApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Skill Pipelines
        services.AddScoped<IToolPipeline, AdoTaskSyncPipeline>();

        return services;
    }
}
