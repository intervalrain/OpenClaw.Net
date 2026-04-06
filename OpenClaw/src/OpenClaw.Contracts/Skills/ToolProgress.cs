namespace OpenClaw.Contracts.Skills;

/// <summary>
/// Progress event emitted during streaming tool execution.
/// Ref: Claude Code StreamingToolExecutor — tools yield progress during execution.
/// </summary>
public record ToolProgress(ToolProgressType Type, string? Message = null, ToolResult? Result = null);

public enum ToolProgressType
{
    Started,
    InProgress,
    Completed,
    Failed
}
