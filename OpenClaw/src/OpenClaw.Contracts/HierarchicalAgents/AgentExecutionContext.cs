using System.Text.Json;

namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// Execution context passed to an agent, carrying input data, services, options,
/// and an optional parent reference for hierarchy tracking.
/// </summary>
public class AgentExecutionContext
{
    public required JsonDocument Input { get; init; }
    public required IServiceProvider Services { get; init; }
    public required AgentExecutionOptions Options { get; init; }

    /// <summary>Parent context for hierarchy tracking (null if root).</summary>
    public AgentExecutionContext? Parent { get; init; }

    /// <summary>
    /// Current depth in the agent hierarchy (0 = root).
    /// </summary>
    public int Depth => Parent is null ? 0 : Parent.Depth + 1;

    /// <summary>
    /// Accumulated token usage across all agent executions in this context chain.
    /// Thread-safe for parallel DAG node execution.
    /// </summary>
    public decimal TokensUsed
    {
        get { lock (_tokenLock) { return _tokensUsed; } }
    }
    private decimal _tokensUsed;
    private readonly object _tokenLock = new();

    /// <summary>
    /// Add tokens consumed by an agent execution.
    /// </summary>
    public void AddTokensUsed(decimal tokens)
    {
        lock (_tokenLock) { _tokensUsed += tokens; }
    }

    /// <summary>
    /// Execution timeline events for observability.
    /// </summary>
    public AgentExecutionTimeline Timeline { get; } = new();
}

/// <summary>
/// Records timestamped events during agent execution for observability.
/// Thread-safe for parallel DAG execution.
/// </summary>
public class AgentExecutionTimeline
{
    private readonly List<AgentTimelineEvent> _events = [];
    private readonly object _lock = new();

    public void Record(string agentName, AgentTimelineEventType type, string? detail = null)
    {
        lock (_lock)
        {
            _events.Add(new AgentTimelineEvent
            {
                Timestamp = DateTime.UtcNow,
                AgentName = agentName,
                Type = type,
                Detail = detail
            });
        }
    }

    public IReadOnlyList<AgentTimelineEvent> GetEvents()
    {
        lock (_lock)
        {
            return _events.ToList();
        }
    }
}

public record AgentTimelineEvent
{
    public required DateTime Timestamp { get; init; }
    public required string AgentName { get; init; }
    public required AgentTimelineEventType Type { get; init; }
    public string? Detail { get; init; }
}

public enum AgentTimelineEventType
{
    Started,
    Completed,
    Failed,
    Skipped,
    ToolCallStarted,
    ToolCallCompleted,
    LlmCallStarted,
    LlmCallCompleted
}
