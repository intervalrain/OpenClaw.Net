namespace OpenClaw.Contracts.Updates.Responses;

public record SystemUpdateResponse
{
    public required Guid Id { get; init; }
    public required string TagName { get; init; }
    public required string ReleaseName { get; init; }
    public string? ReleaseNotes { get; init; }
    public string? HtmlUrl { get; init; }
    public required DateTime PublishedAt { get; init; }
    public required bool IsAcknowledged { get; init; }
    public required bool IsDismissed { get; init; }
    public required DateTime DetectedAt { get; init; }
}
