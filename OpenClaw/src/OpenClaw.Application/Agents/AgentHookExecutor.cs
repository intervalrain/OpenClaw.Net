using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents;

/// <summary>
/// Executes all registered hooks for a given event. Errors in hooks are logged
/// but never block pipeline execution (fire-and-forget semantics).
/// </summary>
public class AgentHookExecutor(IEnumerable<IAgentHook> hooks, ILogger<AgentHookExecutor> logger)
{
    public async Task ExecutePreToolAsync(ToolExecutionHookContext context, CancellationToken ct = default)
        => await ExecuteAllAsync(h => h.OnPreToolExecutionAsync(context, ct), "PreToolExecution");

    public async Task ExecutePostToolAsync(ToolExecutionHookContext context, CancellationToken ct = default)
        => await ExecuteAllAsync(h => h.OnPostToolExecutionAsync(context, ct), "PostToolExecution");

    public async Task ExecuteLlmCallStartAsync(LlmCallHookContext context, CancellationToken ct = default)
        => await ExecuteAllAsync(h => h.OnLlmCallStartAsync(context, ct), "LlmCallStart");

    public async Task ExecuteLlmCallCompleteAsync(LlmCallHookContext context, CancellationToken ct = default)
        => await ExecuteAllAsync(h => h.OnLlmCallCompleteAsync(context, ct), "LlmCallComplete");

    public async Task ExecutePipelineStartAsync(PipelineHookContext context, CancellationToken ct = default)
        => await ExecuteAllAsync(h => h.OnPipelineStartAsync(context, ct), "PipelineStart");

    public async Task ExecutePipelineCompleteAsync(PipelineHookContext context, CancellationToken ct = default)
        => await ExecuteAllAsync(h => h.OnPipelineCompleteAsync(context, ct), "PipelineComplete");

    public async Task ExecuteErrorAsync(ErrorHookContext context, CancellationToken ct = default)
        => await ExecuteAllAsync(h => h.OnErrorAsync(context, ct), "Error");

    private async Task ExecuteAllAsync(Func<IAgentHook, Task> action, string eventName)
    {
        foreach (var hook in hooks)
        {
            try
            {
                await action(hook);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Hook] {Event} hook {Hook} failed: {Message}",
                    eventName, hook.GetType().Name, ex.Message);
            }
        }
    }
}
