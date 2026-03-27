using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Pipelines;

public enum PipelineStreamEventType
{
    StepStarted,
    StepCompleted,
    ApprovalRequired,
    Completed,
    Failed
}

public record PipelineStreamEvent(
    PipelineStreamEventType Type,
    string? StepName = null,
    ToolStepResult? StepResult = null,
    PipelineApprovalRequest? ApprovalRequest = null,
    ToolPipelineResult? FinalResult = null,
    string? Error = null);
