namespace OpenClaw.Contracts.SkillStore.Responses;

public record SkillListingResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string Version { get; init; }
    public string? IconUrl { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? Category { get; init; }
    public string? Tags { get; init; }
    public required string Status { get; init; }
    public string? ReviewComment { get; init; }
    public required Guid AuthorUserId { get; init; }
    public required string AuthorName { get; init; }
    public required int StarCount { get; init; }
    public required int FollowerCount { get; init; }
    public required int DownloadCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool IsStarred { get; init; }
    public bool IsFollowed { get; init; }
    public bool IsInstalled { get; init; }
    public string? InstalledVersion { get; init; }
    public bool HasUpdate { get; init; }
}

public record SkillListingSummaryResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string Version { get; init; }
    public string? IconUrl { get; init; }
    public string? Category { get; init; }
    public required string AuthorName { get; init; }
    public required int StarCount { get; init; }
    public required int DownloadCount { get; init; }
}

public record SkillReviewResponse
{
    public required Guid Id { get; init; }
    public required Guid SkillListingId { get; init; }
    public required string ReviewerName { get; init; }
    public required string Decision { get; init; }
    public string? Comment { get; init; }
    public required string VersionReviewed { get; init; }
    public required DateTime CreatedAt { get; init; }
}
