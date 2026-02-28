namespace OpenClaw.Channels.Telegram;

public class TelegramBotOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Enable or disable the Telegram channel adapter.</summary>
    public bool Enabled { get; set; }

    /// <summary>Telegram Bot API token from @BotFather.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Webhook URL. If set, uses webhook mode; otherwise uses polling mode.
    /// Example: https://yourdomain.com/api/v1/telegram/webhook
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Secret token for webhook verification.</summary>
    public string? SecretToken { get; set; }

    /// <summary>
    /// Allowed Telegram user IDs. Empty array means all users are allowed.
    /// </summary>
    public long[] AllowedUserIds { get; set; } = [];
}
