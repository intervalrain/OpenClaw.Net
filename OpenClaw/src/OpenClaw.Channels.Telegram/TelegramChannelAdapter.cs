using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenClaw.Contracts.Channels;

using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace OpenClaw.Channels.Telegram;

public class TelegramChannelAdapter(
    TelegramBotClient botClient,
    TelegramMessageHandler messageHandler,
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramChannelAdapter> logger) : BackgroundService, IChannelAdapter
{
    private readonly TelegramBotOptions _options = options.Value;

    public string Name => "telegram";
    public string DisplayName => "Telegram Bot";
    public ChannelAdapterStatus Status { get; private set; } = ChannelAdapterStatus.Stopped;

    public async Task SendMessageAsync(string externalId, string message, CancellationToken ct = default)
    {
        if (Status != ChannelAdapterStatus.Running)
            throw new InvalidOperationException("Telegram channel adapter is not running.");

        if (!long.TryParse(externalId, out var chatId))
            throw new ArgumentException($"Invalid Telegram chat ID: {externalId}", nameof(externalId));

        await botClient.SendMessage(chatId, message, cancellationToken: ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            Status = ChannelAdapterStatus.Disabled;
            logger.LogInformation("Telegram channel adapter is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            Status = ChannelAdapterStatus.Error;
            logger.LogError("Telegram BotToken is not configured");
            return;
        }

        try
        {
            Status = ChannelAdapterStatus.Starting;

            if (!string.IsNullOrWhiteSpace(_options.WebhookUrl))
            {
                await StartWebhookModeAsync(stoppingToken);
            }
            else
            {
                await StartPollingModeAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Status = ChannelAdapterStatus.Error;
            logger.LogError(ex, "Telegram channel adapter encountered an error");
        }
        finally
        {
            Status = ChannelAdapterStatus.Stopped;
        }
    }

    private async Task StartPollingModeAsync(CancellationToken stoppingToken)
    {
        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation("Telegram bot started in polling mode: @{BotUsername} ({BotId})", me.Username, me.Id);

        await botClient.DeleteWebhook(cancellationToken: stoppingToken);
        await botClient.DropPendingUpdates(cancellationToken: stoppingToken);

        botClient.OnError += OnError;
        botClient.OnMessage += OnMessage;
        botClient.OnUpdate += OnUpdate;

        Status = ChannelAdapterStatus.Running;

        // Keep alive until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async Task StartWebhookModeAsync(CancellationToken stoppingToken)
    {
        var me = await botClient.GetMe(stoppingToken);
        logger.LogInformation("Telegram bot started in webhook mode: @{BotUsername} ({BotId}), URL: {WebhookUrl}",
            me.Username, me.Id, _options.WebhookUrl);

        await botClient.SetWebhook(
            _options.WebhookUrl!,
            secretToken: _options.SecretToken,
            cancellationToken: stoppingToken);

        Status = ChannelAdapterStatus.Running;

        // Keep alive until cancellation
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Status == ChannelAdapterStatus.Running && !string.IsNullOrWhiteSpace(_options.WebhookUrl))
        {
            try
            {
                await botClient.DeleteWebhook(cancellationToken: cancellationToken);
                logger.LogInformation("Telegram webhook removed");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to remove Telegram webhook during shutdown");
            }
        }

        await base.StopAsync(cancellationToken);
        logger.LogInformation("Telegram channel adapter stopped");
    }

    private Task OnError(Exception ex, HandleErrorSource source)
    {
        logger.LogError(ex, "Telegram bot error from {Source}", source);
        return Task.CompletedTask;
    }

    private async Task OnMessage(Message message, UpdateType type)
    {
        await messageHandler.HandleMessageAsync(message);
    }

    private async Task OnUpdate(Update update)
    {
        // Handle callback queries or other update types in the future
        if (update.CallbackQuery is not null)
        {
            await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
        }
    }
}
