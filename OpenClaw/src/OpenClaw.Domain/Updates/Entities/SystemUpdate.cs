using Weda.Core.Domain;

namespace OpenClaw.Domain.Updates.Entities;

/// <summary>
/// Tracks detected GitHub releases and their acknowledgment status by super admin.
/// </summary>
public class SystemUpdate : Entity<Guid>
{
    public string TagName { get; private set; } = null!;
    public string ReleaseName { get; private set; } = null!;
    public string? ReleaseNotes { get; private set; }
    public string? HtmlUrl { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public bool IsAcknowledged { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public bool IsDismissed { get; private set; }
    public DateTime DetectedAt { get; private set; }

    private SystemUpdate() : base(Guid.NewGuid()) { }

    public static SystemUpdate Create(
        string tagName,
        string releaseName,
        string? releaseNotes,
        string? htmlUrl,
        DateTime publishedAt)
    {
        return new SystemUpdate
        {
            TagName = tagName,
            ReleaseName = releaseName,
            ReleaseNotes = releaseNotes,
            HtmlUrl = htmlUrl,
            PublishedAt = publishedAt,
            IsAcknowledged = false,
            IsDismissed = false,
            DetectedAt = DateTime.UtcNow,
        };
    }

    public void Acknowledge(Guid userId)
    {
        IsAcknowledged = true;
        AcknowledgedByUserId = userId;
        AcknowledgedAt = DateTime.UtcNow;
    }

    public void Dismiss(Guid userId)
    {
        IsDismissed = true;
        AcknowledgedByUserId = userId;
        AcknowledgedAt = DateTime.UtcNow;
    }
}
