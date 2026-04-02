using Weda.Core.Domain;

namespace OpenClaw.Domain.Channels.Entities;

/// <summary>
/// Maps an external platform user to an OpenClaw user account.
/// Enables channel messages to use the correct user's model provider and workspace.
/// </summary>
public class ChannelUserBinding : Entity<Guid>
{
    public string Platform { get; private set; } = string.Empty;
    public string ExternalUserId { get; private set; } = string.Empty;
    public Guid OpenClawUserId { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ChannelUserBinding() : base(Guid.NewGuid()) { }

    public static ChannelUserBinding Create(
        string platform,
        string externalUserId,
        Guid openClawUserId,
        string? displayName = null)
    {
        return new ChannelUserBinding
        {
            Id = Guid.NewGuid(),
            Platform = platform.ToLowerInvariant(),
            ExternalUserId = externalUserId,
            OpenClawUserId = openClawUserId,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        };
    }
}
