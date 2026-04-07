namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Executes tool operations in an isolated sandbox (e.g., git worktree,
/// temp directory). Changes are diffed and require approval before merge.
///
/// Ref: Claude Code EnterWorktreeTool — git worktree isolation for sub-agents,
/// auto-cleanup if no changes, branch returned if changes exist.
/// </summary>
public interface ISandboxExecutor
{
    /// <summary>
    /// Creates an isolated workspace and returns its path.
    /// </summary>
    Task<SandboxContext> CreateAsync(Guid workspaceId, CancellationToken ct = default);

    /// <summary>
    /// Returns a diff of changes made in the sandbox.
    /// </summary>
    Task<string> GetDiffAsync(SandboxContext context, CancellationToken ct = default);

    /// <summary>
    /// Merges sandbox changes back to the main workspace.
    /// </summary>
    Task MergeAsync(SandboxContext context, CancellationToken ct = default);

    /// <summary>
    /// Discards the sandbox and cleans up.
    /// </summary>
    Task DiscardAsync(SandboxContext context, CancellationToken ct = default);
}

public class SandboxContext
{
    public required string SandboxId { get; init; }
    public required string SandboxPath { get; init; }
    public required string OriginalPath { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
