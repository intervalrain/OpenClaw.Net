using Weda.Core.Domain;

namespace OpenClaw.Domain.Configuration.Entities;

public class ChannelSettings : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string ChannelType { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public string? EncryptedBotToken { get; private set; }
    public string? WebhookUrl { get; private set; }
    public string? SecretToken { get; private set; }
    public string AllowedUserIds { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ChannelSettings() : base(Guid.NewGuid()) { }

    public static ChannelSettings Create(
        Guid userId,
        string channelType,
        bool enabled = false,
        string? encryptedBotToken = null,
        string? webhookUrl = null,
        string? secretToken = null,
        string? allowedUserIds = null)
    {
        return new ChannelSettings
        {
            UserId = userId,
            ChannelType = channelType,
            Enabled = enabled,
            EncryptedBotToken = encryptedBotToken,
            WebhookUrl = webhookUrl,
            SecretToken = secretToken,
            AllowedUserIds = allowedUserIds ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        bool enabled,
        string? encryptedBotToken,
        string? webhookUrl,
        string? secretToken,
        string allowedUserIds)
    {
        Enabled = enabled;
        if (encryptedBotToken is not null)
            EncryptedBotToken = encryptedBotToken;
        WebhookUrl = webhookUrl;
        SecretToken = secretToken;
        AllowedUserIds = allowedUserIds;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        Enabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        Enabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public List<long> GetAllowedUserIdsList()
    {
        if (string.IsNullOrWhiteSpace(AllowedUserIds))
            return [];

        return AllowedUserIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => long.TryParse(s, out _))
            .Select(long.Parse)
            .ToList();
    }

    public void SetAllowedUserIds(IEnumerable<long> userIds)
    {
        AllowedUserIds = string.Join(",", userIds);
        UpdatedAt = DateTime.UtcNow;
    }
}
