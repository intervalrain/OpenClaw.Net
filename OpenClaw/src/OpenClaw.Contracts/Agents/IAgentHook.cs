namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Event-driven hook for agent pipeline extensibility.
/// Hooks are notified at key lifecycle points but cannot block execution
/// (fire-and-forget). Use middleware for control flow changes.
///
/// Ref: Claude Code hooks.ts — 20+ hook events (preToolUse, postToolUse,
/// sessionStart, permissionRequest, etc.)
/// </summary>
public interface IAgentHook
{
    Task OnPreToolExecutionAsync(ToolExecutionHookContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    Task OnPostToolExecutionAsync(ToolExecutionHookContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    Task OnLlmCallStartAsync(LlmCallHookContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    Task OnLlmCallCompleteAsync(LlmCallHookContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    Task OnPipelineStartAsync(PipelineHookContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    Task OnPipelineCompleteAsync(PipelineHookContext context, CancellationToken ct = default)
        => Task.CompletedTask;

    Task OnErrorAsync(ErrorHookContext context, CancellationToken ct = default)
        => Task.CompletedTask;
}

public record ToolExecutionHookContext(
    string ToolName,
    string? Arguments,
    string? Result = null,
    bool IsSuccess = true,
    Guid? UserId = null);

public record LlmCallHookContext(
    string ProviderName,
    int MessageCount,
    Llm.LlmUsage? Usage = null);

public record PipelineHookContext(
    string UserInput,
    string? Result = null,
    Guid? UserId = null);

public record ErrorHookContext(
    string Source,
    string ErrorMessage,
    string? ErrorType = null);
