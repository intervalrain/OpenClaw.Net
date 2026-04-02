using Weda.Core.Domain;

namespace OpenClaw.Domain.SkillStore.Entities;

/// <summary>
/// Tracks which users have installed a skill listing and which version they have.
/// Users with installations are notified when updates are available.
/// </summary>
public class SkillInstallation : Entity<Guid>, IUserScoped
{
    public Guid SkillListingId { get; private set; }
    public Guid UserId { get; private set; }
    public string InstalledVersion { get; private set; } = null!;
    public bool HasUpdate { get; private set; }
    public DateTime InstalledAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public Guid GetOwnerUserId() => UserId;

    private SkillInstallation() : base(Guid.NewGuid()) { }

    public static SkillInstallation Create(Guid skillListingId, Guid userId, string version)
    {
        return new SkillInstallation
        {
            SkillListingId = skillListingId,
            UserId = userId,
            InstalledVersion = version,
            HasUpdate = false,
            InstalledAt = DateTime.UtcNow,
        };
    }

    public void UpgradeTo(string newVersion)
    {
        InstalledVersion = newVersion;
        HasUpdate = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkUpdateAvailable()
    {
        HasUpdate = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
