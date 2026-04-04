using System.Text.Json;

namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// A single node in a task DAG, representing one agent execution.
/// </summary>
public class TaskNode
{
    public required string Id { get; init; }
    public required string AgentName { get; init; }
    public JsonDocument? Input { get; set; }

    public TaskNodeStatus Status { get; set; } = TaskNodeStatus.Pending;
    public JsonDocument? Output { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal TokensUsed { get; set; }
}
