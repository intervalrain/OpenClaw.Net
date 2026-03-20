using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows.Responses;

namespace OpenClaw.Contracts.Workflows.Queries;

/// <summary>
/// Query to list all workflow definitions.
/// </summary>
public record GetWorkflowsQuery(
    Guid? UserId = null,
    bool? IsActive = null) : IRequest<ErrorOr<IReadOnlyList<WorkflowSummaryResponse>>>;

/// <summary>
/// Query to get a specific workflow definition.
/// </summary>
public record GetWorkflowQuery(Guid WorkflowId) : IRequest<ErrorOr<WorkflowDefinitionResponse>>;

/// <summary>
/// Query to list executions, optionally filtered by workflow.
/// </summary>
public record GetWorkflowExecutionsQuery(
    Guid? WorkflowId = null,
    int Limit = 20,
    int Offset = 0) : IRequest<ErrorOr<IReadOnlyList<WorkflowExecutionSummary>>>;

/// <summary>
/// Query to get a specific execution with all node statuses.
/// </summary>
public record GetWorkflowExecutionQuery(
    Guid ExecutionId) : IRequest<ErrorOr<WorkflowExecutionResponse>>;

/// <summary>
/// Query to get a specific node's execution result.
/// </summary>
public record GetNodeExecutionQuery(
    Guid ExecutionId,
    string NodeId) : IRequest<ErrorOr<NodeExecutionResponse>>;
