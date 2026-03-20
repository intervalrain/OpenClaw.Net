using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Responses;

namespace OpenClaw.Contracts.Workflows.Commands;

public record CreateWorkflowCommand(
    string Name,
    string? Description,
    WorkflowGraph Definition,
    ScheduleConfig? Schedule,
    Guid UserId) : IRequest<ErrorOr<WorkflowDefinitionResponse>>;