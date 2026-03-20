using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Commands;
using OpenClaw.Domain.Workflows;

namespace OpenClaw.Application.Workflows.Commands;

public class ExecuteWorkflowCommandHandler(
    IWorkflowDefinitionRepository repository,
    IWorkflowExecutor executor) : IRequestHandler<ExecuteWorkflowCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(
        ExecuteWorkflowCommand request,
        CancellationToken ct)
    {
        var workflow = await repository.GetByIdAsync(request.WorkflowId, ct);
        if (workflow is null)
        {
            return Error.NotFound($"Workflow {request.WorkflowId} not found");
        }

        if (!workflow.IsActive)
        {
            return Error.Validation("Cannot execute inactive workflow");
        }

        var executionId = await executor.StartAsync(
            workflow,
            request.InputJson,
            request.UserId,
            request.Trigger,
            ct);

        return executionId;
    }
}
