namespace ClawOS.Contracts.CronJobs.Responses;

/// <summary>
/// Response DTO for cron job execution details.
/// </summary>
public record CronJobExecutionResponse
{
    public required Guid Id { get; init; }
    public required Guid CronJobId { get; init; }
    public required string Status { get; init; }
    public required string Trigger { get; init; }
    public string? OutputText { get; init; }
    public string? ToolCallsJson { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}
