namespace OpenClaw.Contracts.SkillStore.Requests;

public record PublishSkillRequest
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string Version { get; init; }
    public required string ContentJson { get; init; }
    public string? IconUrl { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? Category { get; init; }
    public string? Tags { get; init; }
}

public record UpdateSkillListingRequest
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? IconUrl { get; init; }
    public string? RepositoryUrl { get; init; }
    public string? Category { get; init; }
    public string? Tags { get; init; }
}

public record PublishNewVersionRequest
{
    public required string Version { get; init; }
    public required string ContentJson { get; init; }
}

public record ReviewSkillRequest
{
    public required string Decision { get; init; }
    public string? Comment { get; init; }
}
