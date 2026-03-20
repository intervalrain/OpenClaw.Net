using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Responses;

namespace OpenClaw.Contracts.Workflows.Commands;

public record CloneWorkflowCommand(
    Guid SourceWorkflowId,
    string? NewName,
    Guid UserId) : IRequest<ErrorOr<WorkflowDefinitionResponse>>;