namespace ClawOS.Domain.CronJobs;

/// <summary>
/// Determines how a cron job can be triggered.
/// </summary>
public enum WakeMode
{
    /// <summary>
    /// Only triggered by the scheduler based on the cron schedule.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Only triggered manually by the user.
    /// </summary>
    Manual,

    /// <summary>
    /// Can be triggered both by the scheduler and manually.
    /// </summary>
    Both
}

/// <summary>
/// Status of a cron job execution.
/// </summary>
public enum CronJobExecutionStatus
{
    /// <summary>
    /// Execution is queued but not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Execution is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Execution completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Execution failed due to an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Execution was cancelled by user.
    /// </summary>
    Cancelled
}

/// <summary>
/// How the cron job execution was triggered.
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
