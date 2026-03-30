namespace OpenClaw.Contracts.CronJobs.Events;

/// <summary>
/// Published to NATS JetStream when a cron job is due for execution.
/// Consumed by CronJobEventController workers in a competing consumer pattern.
/// </summary>
public record CronJobExecuteEvent(
    Guid JobId,
    Guid? UserId,
    string Trigger,
    int RetryCount = 0
);
