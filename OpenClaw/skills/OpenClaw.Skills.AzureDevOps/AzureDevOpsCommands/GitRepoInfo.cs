namespace OpenClaw.Skills.AzureDevOps.AzureDevOpsCommands;

/// <summary>
/// Represents a local git repository with its ADO remote information.
/// </summary>
public record GitRepoInfo(
    string LocalPath,
    string? RemoteUrl,
    string? Organization,
    string? Project,
    string? Repository,
    bool IsAdoRepo
)
{
    public string? AdoRepoPath => IsAdoRepo ? $"{Organization}/{Project}/_git/{Repository}" : null;
}
