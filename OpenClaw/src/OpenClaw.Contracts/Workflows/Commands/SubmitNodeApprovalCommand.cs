using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.Workflows.Commands;

/// <summary>
/// Command to approve or reject a node in a workflow execution.
/// </summary>
public record SubmitNodeApprovalCommand(
    Guid ExecutionId,
    string NodeId,
    bool Approved,
    Guid? ApproverUserId) : IRequest<ErrorOr<bool>>;
