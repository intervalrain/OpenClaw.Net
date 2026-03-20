using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Commands;
using OpenClaw.Domain.Workflows;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Workflows.Commands;

public class DeleteWorkflowCommandHandler(
    IWorkflowDefinitionRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteWorkflowCommand, ErrorOr<bool>>
{
    public async ValueTask<ErrorOr<bool>> Handle(
        DeleteWorkflowCommand request,
        CancellationToken ct)
    {
        var workflow = await repository.GetByIdAsync(request.WorkflowId, ct);
        if (workflow is null)
        {
            return Error.NotFound($"Workflow {request.WorkflowId} not found");
        }

        // Optional: Check if user owns the workflow
        if (workflow.CreatedByUserId != request.UserId)
        {
            return Error.Forbidden("You can only delete your own workflows");
        }

        await repository.DeleteAsync(workflow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return true;
    }
}
