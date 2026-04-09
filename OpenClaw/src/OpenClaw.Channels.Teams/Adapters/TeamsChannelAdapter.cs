using System.Collections.Concurrent;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenClaw.Channels.Teams.Handlers;
using OpenClaw.Channels.Teams.Models;
using OpenClaw.Contracts.Channels;
using OpenClaw.Contracts.Security;
using OpenClaw.Domain.Configuration.Repositories;

namespace OpenClaw.Channels.Teams.Adapters;

public class TeamsChannelAdapter(
    IServiceScopeFactory scopeFactory,
    IOptions<TeamsBotOptions> fallbackOptions,
    ILogger<TeamsChannelAdapter> logger) : BackgroundService, IChannelAdapter
{
    private CloudAdapter? _adapter;
    private TeamsBotOptions? _options;

    public string Name => "teams";
    public string DisplayName => "Microsoft Teams Bot";
    public ChannelAdapterStatus Status { get; private set; } = ChannelAdapterStatus.Stopped;

    /// <summary>Bot Framework HTTP adapter for processing incoming requests.</summary>
    public CloudAdapter? Adapter => _adapter;

    /// <summary>Activity handler with stored conversation references.</summary>
    public TeamsActivityHandler? ActivityHandler { get; private set; }

    public async Task SendMessageAsync(string externalId, string message, CancellationToken ct = default)
    {
        if (Status != ChannelAdapterStatus.Running || _adapter is null || ActivityHandler is null)
            throw new InvalidOperationException("Teams channel adapter is not running.");

        if (!ActivityHandler.ConversationReferences.TryGetValue(externalId, out var reference))
            throw new InvalidOperationException($"No conversation reference found for {externalId}. The user must send a message first.");

        await _adapter.ContinueConversationAsync(
            _options!.AppId,
            reference,
            async (turnContext, cancellationToken) =>
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(message), cancellationToken);
            },
            ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        _options = await LoadSettingsAsync(stoppingToken);

        if (!_options.Enabled)
        {
            Status = ChannelAdapterStatus.Disabled;
            logger.LogInformation("Teams channel adapter is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.AppId) || string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            Status = ChannelAdapterStatus.Error;
            logger.LogError("Teams AppId or AppPassword is not configured");
            return;
        }

        try
        {
            Status = ChannelAdapterStatus.Starting;

            // Create Bot Framework adapter with credentials via ConfigurationBotFrameworkAuthentication
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MicrosoftAppId"] = _options.AppId,
                    ["MicrosoftAppPassword"] = _options.AppPassword,
                    ["MicrosoftAppTenantId"] = _options.TenantId ?? ""
                })
                .Build();

            var botAuth = new ConfigurationBotFrameworkAuthentication(config);
            _adapter = new CloudAdapter(botAuth, logger);

            // Set up error handler
            _adapter.OnTurnError = async (turnContext, exception) =>
            {
                logger.LogError(exception, "Teams bot turn error");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Sorry, something went wrong. Please try again."),
                    stoppingToken);
            };

            // Create activity handler
            using var scope = scopeFactory.CreateScope();
            ActivityHandler = scope.ServiceProvider.GetRequiredService<TeamsActivityHandler>();

            Status = ChannelAdapterStatus.Running;
            logger.LogInformation("Teams channel adapter started (AppId: {AppId})", _options.AppId);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Status = ChannelAdapterStatus.Error;
            logger.LogError(ex, "Teams channel adapter encountered an error");
        }
        finally
        {
            Status = ChannelAdapterStatus.Stopped;
        }
    }

    private async Task<TeamsBotOptions> LoadSettingsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IChannelSettingsRepository>();
            var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            var allSettings = await repository.GetAllEnabledByChannelTypeAsync("teams", ct);

            if (allSettings.Count == 0)
            {
                logger.LogInformation("No enabled Teams settings in database, using appsettings fallback");
                return fallbackOptions.Value;
            }

            var primary = allSettings.FirstOrDefault(s => !string.IsNullOrEmpty(s.EncryptedBotToken));
            if (primary is null)
            {
                logger.LogInformation("No Teams settings with credentials found, using appsettings fallback");
                return fallbackOptions.Value;
            }

            // EncryptedBotToken stores AppPassword for Teams
            var appPassword = encryption.Decrypt(primary.EncryptedBotToken!);

            // SecretToken stores AppId for Teams (not a secret, but reuse existing field)
            var appId = primary.SecretToken ?? fallbackOptions.Value.AppId;

            logger.LogInformation("Loaded Teams settings from database");

            return new TeamsBotOptions
            {
                Enabled = true,
                AppId = appId,
                AppPassword = appPassword,
                TenantId = primary.WebhookUrl // Reuse WebhookUrl field for TenantId
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load Teams settings from database, using appsettings fallback");
            return fallbackOptions.Value;
        }
    }
}
