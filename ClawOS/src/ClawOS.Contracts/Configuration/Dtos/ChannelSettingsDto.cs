namespace ClawOS.Contracts.Configuration.Dtos;

public record ChannelSettingsDto(
    Guid Id,
    string ChannelType,
    bool Enabled,
    string? BotTokenMasked,
    string? WebhookUrl,
    string? SecretToken,
    List<long> AllowedUserIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
