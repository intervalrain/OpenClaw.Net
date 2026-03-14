using ErrorOr;

using Mediator;

using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Pipelines;
using OpenClaw.Contracts.Pipelines.Commands;
using OpenClaw.Contracts.Pipelines.Responses;

namespace OpenClaw.Application.Pipelines.Commands;

public class SubmitApprovalCommandHandler(
    IPipelineExecutionStore executionStore,
    ILogger<SubmitApprovalCommandHandler> logger) : IRequestHandler<SubmitApprovalCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(SubmitApprovalCommand command, CancellationToken ct)
    {
        var execution = await executionStore.GetAsync(command.ExecutionId, ct);
        if (execution == null)
        {
            return Error.NotFound("Execution.NotFound", $"Execution '{command.ExecutionId}' not found");
        }

        if (execution.Status != PipelineExecutionStatus.WaitingForApproval)
        {
            return Error.Conflict("Execution.NotWaiting", $"Execution is not waiting for approval (status: {execution.Status})");
        }

        var success = await executionStore.SubmitApprovalAsync(command.ExecutionId, command.Approved, ct);
        if (!success)
        {
            return Error.Failure("Approval.Failed", "Failed to submit approval");
        }

        logger.LogInformation("Approval submitted for {ExecutionId}: {Approved}", command.ExecutionId, command.Approved);

        return Result.Success;
    }
}
