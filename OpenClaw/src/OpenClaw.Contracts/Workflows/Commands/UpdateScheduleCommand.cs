using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Responses;

namespace OpenClaw.Contracts.Workflows.Commands;

public record UpdateScheduleCommand(
    Guid WorkflowId,
    ScheduleConfig Schedule,
    Guid UserId) : IRequest<ErrorOr<WorkflowDefinitionResponse>>;
