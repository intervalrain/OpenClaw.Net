namespace OpenClaw.Contracts.CronJobs.Requests;

/// <summary>
/// Request to create a new cron job.
/// </summary>
public record CreateCronJobRequest
{
    public required string Name { get; init; }
    public string? ScheduleJson { get; init; }
    public Guid? SessionId { get; init; }
    public required string WakeMode { get; init; }
    public string? ContextJson { get; init; }
    public required string Content { get; init; }
}

/// <summary>
/// Request to update an existing cron job.
/// </summary>
public record UpdateCronJobRequest
{
    public string? Name { get; init; }
    public string? ScheduleJson { get; init; }
    public Guid? SessionId { get; init; }
    public string? WakeMode { get; init; }
    public string? ContextJson { get; init; }
    public string? Content { get; init; }
    public bool? IsActive { get; init; }
}

/// <summary>
/// Request to create a new tool instance.
/// </summary>
public record CreateToolInstanceRequest
{
    public required string Name { get; init; }
    public required string ToolName { get; init; }
    public string? ArgsJson { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Request to update an existing tool instance.
/// </summary>
public record UpdateToolInstanceRequest
{
    public string? Name { get; init; }
    public string? ToolName { get; init; }
    public string? ArgsJson { get; init; }
    public string? Description { get; init; }
}
