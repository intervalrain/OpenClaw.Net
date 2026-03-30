using System.Text.Json;
using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Shared base class for all agent types. Handles depth validation and timeout enforcement.
/// </summary>
public abstract class AgentBase : IAgent
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual string Version => "1.0";
    public abstract AgentExecutionType ExecutionType { get; }
    public virtual string? PreferredProvider => null;
    public virtual JsonDocument? InputSchema => null;
    public virtual JsonDocument? OutputSchema => null;

    public async Task<AgentResult> ExecuteAsync(AgentExecutionContext context, CancellationToken ct = default)
    {
        if (context.Depth >= context.Options.MaxDepth)
            return AgentResult.Failed($"Max agent depth ({context.Options.MaxDepth}) exceeded.");

        using var cts = context.Options.Timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : null;

        if (cts is not null)
            cts.CancelAfter(context.Options.Timeout!.Value);

        var token = cts?.Token ?? ct;

        try
        {
            return await ExecuteCoreAsync(context, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return AgentResult.Failed($"Agent '{Name}' timed out after {context.Options.Timeout}.");
        }
        catch (OperationCanceledException)
        {
            return AgentResult.Cancelled();
        }
        catch (Exception ex)
        {
            return AgentResult.Failed($"Agent '{Name}' failed: {ex.Message}");
        }
    }

    protected abstract Task<AgentResult> ExecuteCoreAsync(AgentExecutionContext context, CancellationToken ct);
}
