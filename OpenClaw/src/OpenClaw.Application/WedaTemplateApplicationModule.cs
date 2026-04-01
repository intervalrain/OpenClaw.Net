using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Application.Channels;
using OpenClaw.Application.CronJobs.Tools;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application;

public static class WedaTemplateApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ChannelLinkService>();
        services.AddSingleton<IAgentTool, ManageCronJobTool>();
        return services;
    }
}
