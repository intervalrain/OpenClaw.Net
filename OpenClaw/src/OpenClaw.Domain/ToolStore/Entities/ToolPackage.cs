using OpenClaw.Domain.ToolStore.Enums;
using Weda.Core.Domain;

namespace OpenClaw.Domain.ToolStore.Entities;

/// <summary>
/// Represents an official tool package available in the Tool Store.
/// Managed by super admin — install/uninstall controls which tools are active.
/// </summary>
public class ToolPackage : Entity<Guid>
{
    public string PackageId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? Author { get; private set; }
    public string CurrentVersion { get; private set; } = null!;
    public string? InstalledVersion { get; private set; }
    public ToolPackageStatus Status { get; private set; }
    public string? IconUrl { get; private set; }
    public string? RepositoryUrl { get; private set; }
    public string? Category { get; private set; }
    public DateTime? InstalledAt { get; private set; }
    public Guid? InstalledByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private ToolPackage() : base(Guid.NewGuid()) { }

    public static ToolPackage Create(
        string packageId,
        string name,
        string? description,
        string? author,
        string currentVersion,
        string? iconUrl = null,
        string? repositoryUrl = null,
        string? category = null)
    {
        return new ToolPackage
        {
            PackageId = packageId,
            Name = name,
            Description = description,
            Author = author,
            CurrentVersion = currentVersion,
            Status = ToolPackageStatus.Available,
            IconUrl = iconUrl,
            RepositoryUrl = repositoryUrl,
            Category = category,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void Install(Guid userId)
    {
        InstalledVersion = CurrentVersion;
        Status = ToolPackageStatus.Installed;
        InstalledAt = DateTime.UtcNow;
        InstalledByUserId = userId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Uninstall()
    {
        InstalledVersion = null;
        Status = ToolPackageStatus.Available;
        InstalledAt = null;
        InstalledByUserId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateAvailableVersion(string newVersion)
    {
        CurrentVersion = newVersion;
        if (InstalledVersion != null && InstalledVersion != newVersion)
        {
            Status = ToolPackageStatus.UpdateAvailable;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpgradeToLatest(Guid userId)
    {
        InstalledVersion = CurrentVersion;
        Status = ToolPackageStatus.Installed;
        InstalledByUserId = userId;
        UpdatedAt = DateTime.UtcNow;
    }
}
