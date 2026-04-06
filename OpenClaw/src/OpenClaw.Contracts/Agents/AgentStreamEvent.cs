using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Agents;

public enum AgentStreamEventType
{
    Thinking,
    ToolExecuting,
    ToolProgress,
    ToolCompleted,
    ContentDelta,
    Completed,
    Error,
    ApprovalRequired,
    UsageReport
}

public record AgentStreamEvent(
    AgentStreamEventType Type,
    string? Content = null,
    string? ToolName = null,
    PipelineApprovalRequest? ApprovalRequest = null,
    string? ExecutionId = null,
    LlmUsage? Usage = null);