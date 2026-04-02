namespace OpenClaw.Contracts.ToolStore.Responses;

public record ToolPackageResponse
{
    public required Guid Id { get; init; }
    public required string PackageId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? Author { get; init; }
    public required string CurrentVersion { get; init; }
    public string? InstalledVersion { get; init; }
    public required string Status { get; init; }
    public string? IconUrl { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? Category { get; init; }
    public DateTime? InstalledAt { get; init; }
    public required DateTime CreatedAt { get; init; }
}
