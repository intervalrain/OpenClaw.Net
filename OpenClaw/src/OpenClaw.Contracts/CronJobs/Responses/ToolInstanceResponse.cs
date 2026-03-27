namespace OpenClaw.Contracts.CronJobs.Responses;

/// <summary>
/// Response DTO for tool instance.
/// </summary>
public record ToolInstanceResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string ToolName { get; init; }
    public string? ArgsJson { get; init; }
    public string? Description { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
