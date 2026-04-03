using OpenClaw.Domain.AgentActivities;

namespace OpenClaw.Application.AgentActivities;

public interface IAgentActivityTracker
{
    Task TrackAsync(
        Guid userId,
        string userName,
        ActivityType type,
        ActivityStatus status,
        string? sourceId = null,
        string? sourceName = null,
        string? detail = null,
        CancellationToken ct = default);
}
