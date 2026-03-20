namespace OpenClaw.Contracts.Workflows.Responses;

/// <summary>
/// Response DTO for workflow execution details.
/// </summary>
public record WorkflowExecutionResponse
{
    public required Guid Id { get; init; }
    public required Guid WorkflowDefinitionId { get; init; }
    public required string WorkflowName { get; init; }
    public Guid? UserId { get; init; }
    public required WorkflowExecutionStatus Status { get; init; }
    public required ExecutionTrigger Trigger { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>
    /// Execution status of all nodes.
    /// </summary>
    public required IReadOnlyList<NodeExecutionResponse> NodeExecutions { get; init; }

    /// <summary>
    /// Current pending approval info, if any.
    /// </summary>
    public WorkflowApprovalInfo? PendingApproval { get; init; }
}

/// <summary>
/// Response DTO for individual node execution.
/// </summary>
public record NodeExecutionResponse
{
    public required Guid Id { get; init; }
    public required string NodeId { get; init; }
    public string? NodeLabel { get; init; }
    public required string NodeType { get; init; }
    public required NodeExecutionStatus Status { get; init; }
    public string? InputJson { get; init; }
    public string? OutputJson { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;
}

/// <summary>
/// Information about a pending approval.
/// </summary>
public record WorkflowApprovalInfo
{
    public required string NodeId { get; init; }
    public required string ApprovalName { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// Summary of upstream node results for approval context.
    /// </summary>
    public IReadOnlyList<ApprovalContextItem>? Context { get; init; }
}

/// <summary>
/// Context item shown to approver (summary of upstream node results).
/// </summary>
public record ApprovalContextItem
{
    public required string NodeId { get; init; }
    public required string NodeLabel { get; init; }
    public string? OutputSummary { get; init; }
}