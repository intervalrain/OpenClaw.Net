namespace OpenClaw.Contracts.ToolStore.Requests;

public record SyncToolPackagesRequest
{
    public required string RegistryUrl { get; init; }
}
