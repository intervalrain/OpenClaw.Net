using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using OpenClaw.Channels.Telegram.Adapters;
using OpenClaw.Channels.Telegram.Models;
using OpenClaw.Channels.Telegram.Services;
using OpenClaw.Contracts.Channels;

using Telegram.Bot;

using Weda.Core.Infrastructure.Messaging.Nats.Configuration;

namespace OpenClaw.Channels.Telegram.Extensions;

public static class TelegramServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramChannel(this IServiceCollection services, IConfiguration configuration)
    {
        // Register TelegramBotOptions for fallback (from appsettings)
        services.Configure<TelegramBotOptions>(configuration.GetSection(TelegramBotOptions.SectionName));

        // TelegramChannelAdapter will load settings from database at runtime
        // and create TelegramBotClient dynamically
        services.AddSingleton<TelegramConversationMapper>();
        services.AddSingleton<TelegramChannelAdapter>();
        services.AddSingleton<IChannelAdapter>(sp => sp.GetRequiredService<TelegramChannelAdapter>());
        services.AddHostedService(sp => sp.GetRequiredService<TelegramChannelAdapter>());

        // Register ITelegramBotClient from the adapter's Client property
        services.AddScoped<ITelegramBotClient>(sp =>
        {
            var adapter = sp.GetRequiredService<TelegramChannelAdapter>();
            return adapter.Client ?? throw new InvalidOperationException("Telegram bot client is not available. Adapter may not be running.");
        });

        // Message processing service
        services.AddScoped<ITelegramMessageService, TelegramMessageService>();

        services.AddEventControllers(typeof(TelegramServiceCollectionExtensions).Assembly);

        return services;
    }
}
