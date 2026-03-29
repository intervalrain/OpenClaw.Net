using Weda.Core.Domain;

namespace OpenClaw.Domain.CronJobs.Entities;

/// <summary>
/// Represents a single execution of a cron job.
/// </summary>
public class CronJobExecution : Entity<Guid>
{
    public Guid CronJobId { get; private set; }
    public Guid? UserId { get; private set; }
    public CronJobExecutionStatus Status { get; private set; }
    public ExecutionTrigger Trigger { get; private set; }
    public string? OutputText { get; private set; }
    public string? ToolCallsJson { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // Navigation property
    public CronJob? CronJob { get; private set; }

    private CronJobExecution() : base(Guid.NewGuid()) { }

    public static CronJobExecution Create(
        Guid cronJobId,
        Guid? userId,
        ExecutionTrigger trigger)
    {
        return new CronJobExecution
        {
            Id = Guid.NewGuid(),
            CronJobId = cronJobId,
            UserId = userId,
            Status = CronJobExecutionStatus.Pending,
            Trigger = trigger,
            StartedAt = DateTime.UtcNow
        };
    }

    public void Start()
    {
        Status = CronJobExecutionStatus.Running;
    }

    public void Complete(string? outputText = null, string? toolCallsJson = null)
    {
        Status = CronJobExecutionStatus.Completed;
        OutputText = outputText;
        ToolCallsJson = toolCallsJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string? errorMessage = null)
    {
        Status = CronJobExecutionStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = CronJobExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }
}
