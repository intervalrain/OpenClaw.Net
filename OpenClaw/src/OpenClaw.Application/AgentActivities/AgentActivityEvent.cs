using OpenClaw.Domain.AgentActivities;

namespace OpenClaw.Application.AgentActivities;

/// <summary>
/// In-memory event broadcast for real-time SSE streaming.
/// </summary>
public sealed record AgentActivityEvent(
    Guid Id,
    Guid UserId,
    string UserName,
    ActivityType Type,
    ActivityStatus Status,
    string? SourceId,
    string? SourceName,
    string? Detail,
    DateTime CreatedAt);
