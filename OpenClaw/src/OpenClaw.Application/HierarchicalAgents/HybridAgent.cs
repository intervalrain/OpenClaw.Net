using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Base class for agents that first try deterministic logic, then fall back to LLM if needed.
/// Subclasses implement TryExecuteDeterministicAsync and provide LLM fallback via ExecuteWithLlmAsync.
/// </summary>
public abstract class HybridAgent : LlmAgent
{
    public override AgentExecutionType ExecutionType => AgentExecutionType.Hybrid;

    /// <summary>
    /// Attempt deterministic execution. Return null to fall back to LLM.
    /// </summary>
    protected abstract Task<AgentResult?> TryExecuteDeterministicAsync(
        AgentExecutionContext context, CancellationToken ct);

    protected override async Task<AgentResult> ExecuteCoreAsync(AgentExecutionContext context, CancellationToken ct)
    {
        var deterministicResult = await TryExecuteDeterministicAsync(context, ct);
        if (deterministicResult is not null)
            return deterministicResult;

        // Fall back to LLM execution
        return await base.ExecuteCoreAsync(context, ct);
    }
}
