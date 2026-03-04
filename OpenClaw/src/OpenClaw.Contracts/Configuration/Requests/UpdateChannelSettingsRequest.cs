namespace OpenClaw.Contracts.Configuration.Requests;

public record UpdateChannelSettingsRequest(
    bool Enabled,
    string? BotToken,
    string? WebhookUrl,
    string? SecretToken,
    List<long> AllowedUserIds);
