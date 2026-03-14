using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Pipelines.Responses;

public enum PipelineExecutionStatus
{
    Running,
    WaitingForApproval,
    Completed,
    Failed,
    Rejected
}

public record PipelineExecutionResponse(
    string ExecutionId,
    string PipelineName,
    PipelineExecutionStatus Status,
    string? Summary = null,
    IReadOnlyList<SkillStepResult>? Steps = null,
    PipelineApprovalInfo? ApprovalInfo = null);

public record PipelineApprovalInfo(
    string StepName,
    string Message,
    object? Details = null);

public record ProposedChangeInfo(
    int WorkItemId,
    string CurrentState,
    string ProposedState,
    string Reason);
