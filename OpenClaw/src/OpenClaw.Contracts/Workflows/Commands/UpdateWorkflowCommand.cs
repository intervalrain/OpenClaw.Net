using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Responses;

namespace OpenClaw.Contracts.Workflows.Commands;

public record UpdateWorkflowCommand(
    Guid WorkflowId,
    string? Name,
    string? Description,
    WorkflowGraph? Definition,
    ScheduleConfig? Schedule,
    bool? IsActive,
    Guid UserId) : IRequest<ErrorOr<WorkflowDefinitionResponse>>;