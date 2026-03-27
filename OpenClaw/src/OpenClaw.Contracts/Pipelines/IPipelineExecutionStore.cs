using OpenClaw.Contracts.Pipelines.Responses;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Pipelines;

public interface IPipelineExecutionStore
{
    Task<PipelineExecution> CreateAsync(string pipelineName, string? argsJson, CancellationToken ct);

    Task<PipelineExecution?> GetAsync(string executionId, CancellationToken ct);

    Task<IReadOnlyList<PipelineExecution>> ListRecentAsync(int limit, CancellationToken ct);

    Task UpdateStatusAsync(string executionId, PipelineExecutionStatus status, CancellationToken ct);

    Task SetPendingApprovalAsync(string executionId, PipelineApprovalInfo approvalInfo, CancellationToken ct);

    Task<bool> SubmitApprovalAsync(string executionId, bool approved, CancellationToken ct);

    Task<bool> WaitForApprovalAsync(string executionId, CancellationToken ct);

    Task SetResultAsync(string executionId, ToolPipelineResult result, CancellationToken ct);
}