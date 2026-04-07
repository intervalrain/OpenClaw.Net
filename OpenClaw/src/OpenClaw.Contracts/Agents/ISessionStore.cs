using OpenClaw.Contracts.Llm;

namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Persists and restores agent session state for resume capability.
/// Ref: Claude Code sessions/ — snapshots message state for /resume command.
/// </summary>
public interface ISessionStore
{
    Task<AgentSession?> GetAsync(string sessionId, CancellationToken ct = default);
    Task SaveAsync(AgentSession session, CancellationToken ct = default);
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
}

public class AgentSession
{
    public required string SessionId { get; init; }
    public Guid? UserId { get; init; }
    public Guid? WorkspaceId { get; init; }
    public required List<ChatMessage> Messages { get; init; }
    public LlmUsage TotalUsage { get; init; } = LlmUsage.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
