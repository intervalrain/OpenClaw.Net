using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.Workflows.Commands;

public record DeleteWorkflowCommand(
    Guid WorkflowId,
    Guid UserId) : IRequest<ErrorOr<bool>>;
