namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// A directed acyclic graph of agent tasks.
/// Nodes represent agent executions, edges represent data dependencies.
/// </summary>
public class TaskGraph
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required List<TaskNode> Nodes { get; init; }
    public required List<TaskEdge> Edges { get; init; }
}
