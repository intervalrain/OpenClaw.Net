namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// An edge in a task DAG, connecting an upstream node's output to a downstream node's input.
/// </summary>
public class TaskEdge
{
    public required string FromNodeId { get; init; }
    public required string ToNodeId { get; init; }

    /// <summary>
    /// JSONPath expression to extract a value from the upstream node's output
    /// and map it to the downstream node's input. E.g., "$.result.script"
    /// If null, the entire output is passed.
    /// </summary>
    public string? OutputMapping { get; init; }
}
