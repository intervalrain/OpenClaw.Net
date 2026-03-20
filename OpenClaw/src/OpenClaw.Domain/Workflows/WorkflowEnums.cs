namespace OpenClaw.Domain.Workflows;

/// <summary>
/// Status of a workflow execution.
/// </summary>
public enum WorkflowExecutionStatus
{
    /// <summary>
    /// Workflow is queued but not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Workflow is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Workflow is paused at an approval gate.
    /// </summary>
    WaitingForApproval,

    /// <summary>
    /// Workflow completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Workflow failed due to an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Workflow was cancelled by user.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Workflow was rejected at an approval gate.
    /// </summary>
    Rejected
}

/// <summary>
/// Status of an individual node execution.
/// </summary>
public enum NodeExecutionStatus
{
    /// <summary>
    /// Node has not started execution.
    /// </summary>
    Pending,

    /// <summary>
    /// Node is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Node is waiting for approval (for ApprovalNode only).
    /// </summary>
    WaitingForApproval,

    /// <summary>
    /// Node completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Node failed due to an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Node was skipped (e.g., due to upstream rejection or conditional branching).
    /// </summary>
    Skipped
}

/// <summary>
/// How the workflow execution was triggered.
/// </summary>
public enum ExecutionTrigger
{
    /// <summary>
    /// Manually triggered by user.
    /// </summary>
    Manual,

    /// <summary>
    /// Triggered by scheduler.
    /// </summary>
    Scheduled
}
