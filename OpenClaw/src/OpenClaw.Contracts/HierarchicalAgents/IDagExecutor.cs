namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// Executes a TaskGraph (DAG) by running nodes in dependency order,
/// parallelizing independent nodes.
/// </summary>
public interface IDagExecutor
{
    Task<DagExecutionResult> ExecuteAsync(
        TaskGraph graph,
        AgentExecutionOptions options,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<DagExecutionResult> ExecuteAsync(
        TaskGraph graph,
        AgentExecutionOptions options,
        Guid? userId,
        AgentExecutionTimeline? timeline,
        CancellationToken ct = default);
}

public record DagExecutionResult
{
    public required bool IsSuccess { get; init; }
    public required IReadOnlyList<TaskNode> Nodes { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal TotalTokensUsed { get; init; }
}
