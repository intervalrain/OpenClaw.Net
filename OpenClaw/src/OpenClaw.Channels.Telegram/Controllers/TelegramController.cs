using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenClaw.Channels.Telegram.Models;
using OpenClaw.Contracts.Channels;

using Telegram.Bot.Types;

using Weda.Core.Application.Interfaces.Messaging;
using Weda.Core.Presentation;

namespace OpenClaw.Channels.Telegram.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
public class TelegramController(
    IJetStreamClientFactory factory,
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramController> logger) : ApiController
{
    private readonly TelegramBotOptions _options = options.Value;

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook(
        [FromBody] Update update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken)
    {
        // Verify secret token if configured
        if (!string.IsNullOrWhiteSpace(_options.SecretToken) && secretToken != _options.SecretToken)
        {
            logger.LogWarning("Telegram webhook received with invalid secret token");
            return Unauthorized();
        }

        if (update.Message is not null && update.Message.From is not null)
        {
            var message = update.Message;
            var @event = new ChannelMessageReceivedEvent(
                ChannelName: "telegram",
                ExternalChatId: message.Chat.Id.ToString(),
                ExternalUserId: message.From.Id.ToString(),
                ExternalUsername: message.From.Username ?? message.From.FirstName,
                Content: message.Text,
                ExternalMessageId: message.MessageId.ToString(),
                Type: ChannelMessageType.Text,
                ReceivedAt: DateTimeOffset.UtcNow);

            var client = factory.Create();
            var subject = $"eco1j.weda.{@event.ExternalMessageId}.openclaw.telegram.doc";
            await client.JsPublishAsync(subject, @event);

            logger.LogDebug("Published Telegram message {MessageId} to JetStream", message.MessageId);
        }

        return Ok();
    }
}