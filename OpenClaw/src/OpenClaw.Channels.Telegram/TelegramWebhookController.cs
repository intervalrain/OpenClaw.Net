using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Telegram.Bot.Types;

namespace OpenClaw.Channels.Telegram;

[ApiController]
[Route("api/v1/telegram")]
[AllowAnonymous]
public class TelegramWebhookController(
    TelegramMessageHandler messageHandler,
    IOptions<TelegramBotOptions> options,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    private readonly TelegramBotOptions _options = options.Value;

    [HttpPost("webhook")]
    public IActionResult HandleWebhook(
        [FromBody] Update update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken)
    {
        // Verify secret token if configured
        if (!string.IsNullOrWhiteSpace(_options.SecretToken) && secretToken != _options.SecretToken)
        {
            logger.LogWarning("Telegram webhook received with invalid secret token");
            return Unauthorized();
        }

        // Fire-and-forget: process in background, return 200 immediately
        // Telegram requires fast response (<60s), actual processing may take longer
        if (update.Message is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await messageHandler.HandleMessageAsync(update.Message);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing Telegram webhook update {UpdateId}", update.Id);
                }
            });
        }

        return Ok();
    }
}
