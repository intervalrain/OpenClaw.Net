namespace OpenClaw.Contracts.Notifications.Responses;

public record NotificationResponse
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required string Title { get; init; }
    public string? Message { get; init; }
    public string? ReferenceId { get; init; }
    public required bool IsRead { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}
