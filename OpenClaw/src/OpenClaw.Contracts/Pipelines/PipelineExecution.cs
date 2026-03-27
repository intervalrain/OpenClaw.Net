using OpenClaw.Contracts.Pipelines.Responses;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Pipelines;

public class PipelineExecution
{
    public required string Id { get; init; }
    public required string PipelineName { get; init; }
    public string? ArgsJson { get; init; }
    public PipelineExecutionStatus Status { get; set; }
    public PipelineApprovalInfo? PendingApproval { get; set; }
    public bool? ApprovalDecision { get; set; }
    public ToolPipelineResult? Result { get; set; }
    public DateTime CreatedAt { get; init; }
}