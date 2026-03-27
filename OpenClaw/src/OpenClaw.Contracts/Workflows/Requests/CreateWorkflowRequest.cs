namespace OpenClaw.Contracts.Workflows.Requests;

/// <summary>
/// Request to create a new workflow definition.
/// </summary>
public record CreateWorkflowRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required WorkflowGraph Definition { get; init; }
    public ScheduleConfig? Schedule { get; init; }
}

/// <summary>
/// Request to update an existing workflow definition.
/// </summary>
public record UpdateWorkflowRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public WorkflowGraph? Definition { get; init; }
    public ScheduleConfig? Schedule { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Request to clone an existing workflow.
/// </summary>
public record CloneWorkflowRequest
{
    /// <summary>
    /// Name for the cloned workflow. If not provided, uses "{OriginalName} (Copy)".
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// Request to execute a workflow.
/// </summary>
public record ExecuteWorkflowRequest
{
    /// <summary>
    /// Optional input arguments as JSON.
    /// </summary>
    public string? InputJson { get; init; }

    /// <summary>
    /// Override workflow variables for this execution.
    /// </summary>
    public Dictionary<string, object>? VariableOverrides { get; init; }
}

/// <summary>
/// Request to update workflow schedule.
/// </summary>
public record UpdateScheduleRequest
{
    public required ScheduleConfig Schedule { get; init; }
}

/// <summary>
/// Request to approve a node with optional edited output for downstream nodes.
/// </summary>
public record ApproveNodeRequest
{
    /// <summary>
    /// Edited output JSON to pass to downstream nodes. If null, uses "approved".
    /// </summary>
    public string? EditedOutput { get; init; }
}