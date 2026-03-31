using Weda.Core.Domain;

namespace OpenClaw.Domain.Workspaces.Entities;

/// <summary>
/// Per-directory visibility setting within a user's workspace.
/// Default is Private (only owner can access).
/// </summary>
public class DirectoryPermission : Entity<Guid>
{
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// Relative path within the user's workspace (e.g. "projects/my-app")
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
            RelativePath = relativePath.TrimStart('/').TrimEnd('/'),
            Visibility = visibility,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetVisibility(DirectoryVisibility visibility)
    {
        Visibility = visibility;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum DirectoryVisibility
{
    /// <summary>Only the owner can access</summary>
    Private = 0,

    /// <summary>Anyone can read (download/list), but not write</summary>
    PublicReadonly = 1,

    /// <summary>Anyone can read and write</summary>
    Public = 2
}
