using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Base class for agents that execute pure deterministic logic without LLM calls.
/// </summary>
public abstract class DeterministicAgent : AgentBase
{
    public override AgentExecutionType ExecutionType => AgentExecutionType.Deterministic;
    public override string? PreferredProvider => null;
}
