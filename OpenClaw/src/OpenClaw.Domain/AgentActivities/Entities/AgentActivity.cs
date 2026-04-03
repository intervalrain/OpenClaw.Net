using Weda.Core.Domain;

namespace OpenClaw.Domain.AgentActivities.Entities;

/// <summary>
/// Append-only event log recording agent activity for visualization and replay.
/// </summary>
public class AgentActivity : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public ActivityType Type { get; private set; }
    public ActivityStatus Status { get; private set; }
    public string? SourceId { get; private set; }
    public string? SourceName { get; private set; }
    public string? Detail { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private AgentActivity() : base(Guid.NewGuid()) { }

    public static AgentActivity Create(
        Guid userId,
        string userName,
        ActivityType type,
        ActivityStatus status,
        string? sourceId = null,
        string? sourceName = null,
        string? detail = null)
    {
        return new AgentActivity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            Type = type,
            Status = status,
            SourceId = sourceId,
            SourceName = sourceName,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        };
    }
}
