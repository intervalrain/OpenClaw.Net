using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Commands;
using OpenClaw.Domain.Workflows;

namespace OpenClaw.Application.Workflows.Commands;

public class SubmitNodeApprovalCommandHandler(
    IWorkflowExecutionRepository repository,
    IWorkflowExecutor executor) : IRequestHandler<SubmitNodeApprovalCommand, ErrorOr<bool>>
{
    public async ValueTask<ErrorOr<bool>> Handle(
        SubmitNodeApprovalCommand request,
        CancellationToken ct)
    {
        var execution = await repository.GetByIdAsync(request.ExecutionId, ct);
        if (execution is null)
        {
            return Error.NotFound($"Execution {request.ExecutionId} not found");
        }

        if (execution.Status != WorkflowExecutionStatus.WaitingForApproval)
        {
            return Error.Validation("Execution is not waiting for approval");
        }

        if (execution.PendingApprovalNodeId != request.NodeId)
        {
            return Error.Validation($"Node {request.NodeId} is not pending approval");
        }

        await executor.ResumeAfterApprovalAsync(
            request.ExecutionId,
            request.NodeId,
            request.Approved,
            ct);

        return request.Approved;
    }
}
