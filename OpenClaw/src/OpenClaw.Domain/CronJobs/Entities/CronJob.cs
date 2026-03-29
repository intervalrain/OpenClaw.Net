using Weda.Core.Domain;

namespace OpenClaw.Domain.CronJobs.Entities;

/// <summary>
/// Represents a scheduled cron job that executes a prompt with optional tool context.
/// </summary>
public class CronJob : Entity<Guid>, IUserScoped
{
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Serialized schedule configuration JSON (cron expression, timezone, etc.).
    /// </summary>
    public string ScheduleJson { get; private set; } = string.Empty;

    /// <summary>
    /// Optional session ID to continue an existing conversation.
    /// </summary>
    public Guid? SessionId { get; private set; }

    /// <summary>
    /// Determines how this job can be triggered (Scheduled, Manual, or Both).
    /// </summary>
    public WakeMode WakeMode { get; private set; }

    /// <summary>
    /// JSON array of skill names that provide context for execution.
    /// </summary>
    public string? ContextJson { get; private set; }

    /// <summary>
    /// The prompt text to execute.
    /// </summary>
    public string Content { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Last time this job was executed by the scheduler.
    /// Used for scheduling logic to avoid duplicate runs.
    /// </summary>
    public DateTime? LastScheduledAt { get; private set; }

    // Navigation property
    private readonly List<CronJobExecution> _executions = [];
    public IReadOnlyCollection<CronJobExecution> Executions => _executions.AsReadOnly();

    private CronJob() : base(Guid.NewGuid()) { }

    public static CronJob Create(
        Guid userId,
        string name,
        string scheduleJson,
        string content,
        WakeMode wakeMode = WakeMode.Scheduled,
        Guid? sessionId = null,
        string? contextJson = null)
    {
        return new CronJob
        {
            Id = Guid.NewGuid(),
            Name = name,
            ScheduleJson = scheduleJson,
            SessionId = sessionId,
            WakeMode = wakeMode,
            ContextJson = contextJson,
            Content = content,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Update(
        string? name = null,
        string? scheduleJson = null,
        string? content = null,
        WakeMode? wakeMode = null,
        Guid? sessionId = null,
        string? contextJson = null,
        bool? isActive = null)
    {
        if (name is not null) Name = name;
        if (scheduleJson is not null) ScheduleJson = scheduleJson;
        if (content is not null) Content = content;
        if (wakeMode.HasValue) WakeMode = wakeMode.Value;
        if (sessionId.HasValue) SessionId = sessionId.Value;
        if (contextJson is not null) ContextJson = contextJson;
        if (isActive.HasValue) IsActive = isActive.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkScheduledExecution()
    {
        LastScheduledAt = DateTime.UtcNow;
    }

    public Guid GetOwnerUserId() => CreatedByUserId;
}
