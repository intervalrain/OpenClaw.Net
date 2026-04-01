using ClawOS.Contracts.Skills;

namespace ClawOS.Contracts.Agents;

public enum AgentStreamEventType
{
    Thinking,
    ToolExecuting,
    ToolCompleted,
    ContentDelta,
    Completed,
    Error,
    ApprovalRequired
}

public record AgentStreamEvent(
    AgentStreamEventType Type,
    string? Content = null,
    string? ToolName = null,
    PipelineApprovalRequest? ApprovalRequest = null,
    string? ExecutionId = null);