using System.Text.Json;

namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// A composable autonomous unit that can plan, execute, and produce structured output.
/// Distinct from IAgentTool (which is an LLM-callable function).
/// An Agent is a higher-level concept that may use tools internally.
/// </summary>
public interface IAgent
{
    string Name { get; }
    string Description { get; }
    string Version { get; }
    AgentExecutionType ExecutionType { get; }

    /// <summary>
    /// Preferred LLM provider for this agent. Null means use the default provider.
    /// Pioneer agents use strong models; worker agents use weak/none.
    /// </summary>
    string? PreferredProvider { get; }

    JsonDocument? InputSchema { get; }
    JsonDocument? OutputSchema { get; }

    Task<AgentResult> ExecuteAsync(AgentExecutionContext context, CancellationToken ct = default);
}
