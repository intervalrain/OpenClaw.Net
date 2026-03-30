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
}
