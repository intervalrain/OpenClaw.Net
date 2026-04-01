namespace ClawOS.Contracts.CronJobs.Responses;

/// <summary>
/// Response DTO for cron job definition.
/// </summary>
public record CronJobResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? ScheduleJson { get; init; }
    public Guid? SessionId { get; init; }
    public required string WakeMode { get; init; }
    public string? ContextJson { get; init; }
    public required string Content { get; init; }
    public required bool IsActive { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// Last execution info for quick display.
    /// </summary>
    public CronJobExecutionResponse? LastExecution { get; init; }
}
