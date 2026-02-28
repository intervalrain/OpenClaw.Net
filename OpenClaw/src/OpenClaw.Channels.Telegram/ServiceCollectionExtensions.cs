using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenClaw.Contracts.Channels;

using Telegram.Bot;

namespace OpenClaw.Channels.Telegram;

public static class TelegramServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramChannel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(TelegramBotOptions.SectionName);
        if (!section.GetValue<bool>("Enabled"))
            return services;

        services.Configure<TelegramBotOptions>(section);

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<TelegramBotOptions>>();
            return new TelegramBotClient(opts.Value.BotToken);
        });

        services.AddSingleton<TelegramConversationMapper>();
        services.AddSingleton<TelegramMessageHandler>();
        services.AddSingleton<TelegramChannelAdapter>();
        services.AddSingleton<IChannelAdapter>(sp => sp.GetRequiredService<TelegramChannelAdapter>());
        services.AddHostedService(sp => sp.GetRequiredService<TelegramChannelAdapter>());

        return services;
    }
}
