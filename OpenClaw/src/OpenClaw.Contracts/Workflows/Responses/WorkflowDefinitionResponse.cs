namespace OpenClaw.Contracts.Workflows.Responses;

/// <summary>
/// Response DTO for workflow definition.
/// </summary>
public record WorkflowDefinitionResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required WorkflowGraph Definition { get; init; }
    public ScheduleConfig? Schedule { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>
/// Summary response for workflow list.
/// </summary>
public record WorkflowSummaryResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public ScheduleConfig? Schedule { get; init; }
    public required int NodeCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required bool IsActive { get; init; }

    /// <summary>
    /// Last execution info for quick display.
    /// </summary>
    public WorkflowExecutionSummary? LastExecution { get; init; }
}

/// <summary>
/// Summary of a workflow execution for list display.
/// </summary>
public record WorkflowExecutionSummary
{
    public required Guid Id { get; init; }
    public required WorkflowExecutionStatus Status { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}