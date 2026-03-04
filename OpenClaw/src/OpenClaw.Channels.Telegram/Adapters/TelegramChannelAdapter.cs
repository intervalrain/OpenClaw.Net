using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenClaw.Channels.Telegram.Models;
using OpenClaw.Contracts.Channels;
using OpenClaw.Contracts.Security;
using OpenClaw.Domain.Configuration.Repositories;

using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using Weda.Core.Application.Interfaces.Messaging;

namespace OpenClaw.Channels.Telegram.Adapters;

public class TelegramChannelAdapter(
    IServiceScopeFactory scopeFactory,
    IJetStreamClientFactory factory,
    IOptions<TelegramBotOptions> fallbackOptions,
    ILogger<TelegramChannelAdapter> logger) : BackgroundService, IChannelAdapter
{
    private TelegramBotClient? _client;
    private TelegramBotOptions? _options;

    public string Name => "telegram";
    public string DisplayName => "Telegram Bot";
    public ChannelAdapterStatus Status { get; private set; } = ChannelAdapterStatus.Stopped;

    /// <summary>
    /// Gets the TelegramBotClient instance. May be null if adapter is not running.
    /// </summary>
    public ITelegramBotClient? Client => _client;

    public async Task SendMessageAsync(string externalId, string message, CancellationToken ct = default)
    {
        if (Status != ChannelAdapterStatus.Running || _client is null)
            throw new InvalidOperationException("Telegram channel adapter is not running.");

        if (!long.TryParse(externalId, out var chatId))
            throw new ArgumentException($"Invalid Telegram chat ID: {externalId}", nameof(externalId));

        await _client.SendMessage(chatId, message, cancellationToken: ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for the database to be ready
        await Task.Delay(2000, stoppingToken);

        // Load settings from database, fallback to appsettings
        _options = await LoadSettingsAsync(stoppingToken);

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

            // Create TelegramBotClient with the token
            _client = new TelegramBotClient(_options.BotToken);

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

    private async Task<TelegramBotOptions> LoadSettingsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IChannelSettingsRepository>();
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            var settings = await repository.GetByChannelTypeAsync("telegram", ct);

            if (settings is null || string.IsNullOrEmpty(settings.EncryptedBotToken))
            {
                logger.LogInformation("No Telegram settings in database, using appsettings fallback");
                return fallbackOptions.Value;
            }

            var botToken = encryption.Decrypt(settings.EncryptedBotToken);

            logger.LogInformation("Loaded Telegram settings from database");
            return new TelegramBotOptions
            {
                Enabled = settings.Enabled,
                BotToken = botToken,
                WebhookUrl = settings.WebhookUrl,
                SecretToken = settings.SecretToken,
                AllowedUserIds = settings.GetAllowedUserIdsList().ToArray()
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load Telegram settings from database, using appsettings fallback");
            return fallbackOptions.Value;
        }
    }

    private async Task StartPollingModeAsync(CancellationToken stoppingToken)
    {
        var client = _client!;
        var me = await client.GetMe(stoppingToken);
        logger.LogInformation("Telegram bot started in polling mode: @{BotUsername} ({BotId})", me.Username, me.Id);

        await client.DeleteWebhook(cancellationToken: stoppingToken);
        await client.DropPendingUpdates(cancellationToken: stoppingToken);

        client.OnError += OnError;
        client.OnMessage += OnMessage;
        client.OnUpdate += OnUpdate;

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
        var client = _client!;
        var options = _options!;
        var me = await client.GetMe(stoppingToken);
        logger.LogInformation("Telegram bot started in webhook mode: @{BotUsername} ({BotId}), URL: {WebhookUrl}",
            me.Username, me.Id, options.WebhookUrl);

        await client.SetWebhook(
            options.WebhookUrl!,
            secretToken: options.SecretToken,
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
        if (Status == ChannelAdapterStatus.Running && _client is not null && !string.IsNullOrWhiteSpace(_options?.WebhookUrl))
        {
            try
            {
                await _client.DeleteWebhook(cancellationToken: cancellationToken);
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
        if (message.Text is null || message.From is null)
            return;

        var @event = new ChannelMessageReceivedEvent(
            ChannelName: "telegram",
            ExternalChatId: message.Chat.Id.ToString(),
            ExternalUserId: message.From.Id.ToString(),
            ExternalUsername: message.From.Username ?? message.From.FirstName,
            Content: message.Text,
            ExternalMessageId: message.MessageId.ToString(),
            Type: ChannelMessageType.Text,
            ReceivedAt: DateTimeOffset.UtcNow);

        var bus = factory.Create();
        var subject = $"eco1j.weda.{@event.ExternalMessageId}.openclaw.telegram.doc";
        await bus.JsPublishAsync(subject, @event);

        logger.LogDebug("Published Telegram message {MessageId} to JetStream (polling mode)", message.MessageId);
    }

    private async Task OnUpdate(Update update)
    {
        if (update.CallbackQuery is not null && _client is not null)
        {
            await _client.AnswerCallbackQuery(update.CallbackQuery.Id);
        }
    }
}
