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
    SkillStepResult? StepResult = null,
    PipelineApprovalRequest? ApprovalRequest = null,
    SkillPipelineResult? FinalResult = null,
    string? Error = null);
