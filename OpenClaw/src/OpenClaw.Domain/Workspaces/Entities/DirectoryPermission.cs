using Weda.Core.Domain;

namespace OpenClaw.Domain.Workspaces.Entities;

/// <summary>
/// Per-path visibility setting within a user's workspace.
/// Uses tree-based inheritance: a path inherits from closest parent with explicit setting.
///
/// Resolution order: exact path → parent → grandparent → ... → root → Private (default)
/// </summary>
public class DirectoryPermission : Entity<Guid>
{
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Relative path within the user's workspace (e.g. "projects/my-app").
    /// Empty string = workspace root.
    /// </summary>
    public string RelativePath { get; private set; } = string.Empty;

    public DirectoryVisibility Visibility { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private DirectoryPermission() : base(Guid.NewGuid()) { }

    public static DirectoryPermission Create(Guid ownerUserId, string relativePath, DirectoryVisibility visibility)
    {
        return new DirectoryPermission
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            RelativePath = NormalizePath(relativePath),
            Visibility = visibility,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetVisibility(DirectoryVisibility visibility)
    {
        Visibility = visibility;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resolve effective visibility for a path by walking up the tree.
    /// Returns the first non-Default match, or Private if none found.
    /// </summary>
    public static DirectoryVisibility ResolveEffective(
        string relativePath,
        IReadOnlyList<DirectoryPermission> allPermissions)
    {
        var normalized = NormalizePath(relativePath);

        // Walk from exact path upward to root
        var current = normalized;
        while (true)
        {
            var match = allPermissions.FirstOrDefault(p => p.RelativePath == current);
            if (match is not null && match.Visibility != DirectoryVisibility.Default)
                return match.Visibility;

            // Move to parent
            var lastSlash = current.LastIndexOf('/');
            if (lastSlash <= 0) break;
            current = current[..lastSlash];
        }

        // Check root-level permission (empty string)
        var root = allPermissions.FirstOrDefault(p => p.RelativePath == "");
        if (root is not null && root.Visibility != DirectoryVisibility.Default)
            return root.Visibility;

        return DirectoryVisibility.Private;
    }

    private static string NormalizePath(string path)
        => path.Trim().TrimStart('/').TrimEnd('/');
}

public enum DirectoryVisibility
{
    /// <summary>Inherit from closest parent; Private if no parent has explicit setting</summary>
    Default = 0,

    /// <summary>Anyone can read and write</summary>
    Public = 1,

    /// <summary>Anyone can read (download/list), but not write</summary>
    PublicReadonly = 2,

    /// <summary>Only the owner can access (overrides parent's public)</summary>
    Private = 3
}
