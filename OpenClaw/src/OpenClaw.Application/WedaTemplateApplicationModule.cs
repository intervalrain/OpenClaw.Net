using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Application.Channels;

namespace OpenClaw.Application;

public static class WedaTemplateApplicationModule
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ChannelLinkService>();
        return services;
    }
}
