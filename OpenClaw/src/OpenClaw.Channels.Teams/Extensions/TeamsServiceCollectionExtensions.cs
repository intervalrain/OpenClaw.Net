using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Channels.Teams.Adapters;
using OpenClaw.Channels.Teams.Handlers;
using OpenClaw.Channels.Teams.Models;
using OpenClaw.Channels.Teams.Services;
using OpenClaw.Contracts.Channels;

using Weda.Core.Infrastructure.Messaging.Nats.Configuration;

namespace OpenClaw.Channels.Teams.Extensions;

public static class TeamsServiceCollectionExtensions
{
    public static IServiceCollection AddTeamsChannel(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TeamsBotOptions>(configuration.GetSection(TeamsBotOptions.SectionName));

        services.AddSingleton<TeamsConversationMapper>();
        services.AddSingleton<TeamsActivityHandler>();
        services.AddSingleton<TeamsChannelAdapter>();
        services.AddSingleton<IChannelAdapter>(sp => sp.GetRequiredService<TeamsChannelAdapter>());
        services.AddHostedService(sp => sp.GetRequiredService<TeamsChannelAdapter>());

        services.AddScoped<ITeamsMessageService, TeamsMessageService>();

        services.AddEventControllers(typeof(TeamsServiceCollectionExtensions).Assembly);

        return services;
    }
}
