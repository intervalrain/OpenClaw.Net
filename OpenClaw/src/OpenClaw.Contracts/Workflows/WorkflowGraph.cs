using System.Text.Json.Serialization;

namespace OpenClaw.Contracts.Workflows;

/// <summary>
/// Complete workflow definition as a directed acyclic graph (DAG).
/// </summary>
public record WorkflowGraph
{
    public required List<WorkflowNode> Nodes { get; init; }
    public required List<WorkflowEdge> Edges { get; init; }
}

/// <summary>
/// Edge connecting two nodes in the workflow graph.
/// </summary>
public record WorkflowEdge
{
    public required string Id { get; init; }
    public required string Source { get; init; }
    public required string Target { get; init; }

    /// <summary>
    /// Optional condition expression for conditional branching.
    /// </summary>
    public string? Condition { get; init; }
}

/// <summary>
/// Position of a node in the visual editor.
/// </summary>
public record NodePosition(double X, double Y);