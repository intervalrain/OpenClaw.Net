using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.Workflows.Commands;

/// <summary>
/// Command to start workflow execution.
/// Returns the execution ID immediately; execution runs in background.
/// </summary>
public record ExecuteWorkflowCommand(
    Guid WorkflowId,
    string? InputJson,
    Dictionary<string, object>? VariableOverrides,
    ExecutionTrigger Trigger,
    Guid? UserId) : IRequest<ErrorOr<Guid>>;
