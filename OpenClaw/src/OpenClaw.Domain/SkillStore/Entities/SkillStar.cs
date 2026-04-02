using Weda.Core.Domain;

namespace OpenClaw.Domain.SkillStore.Entities;

/// <summary>
/// Represents a user starring (liking) a skill listing.
/// </summary>
public class SkillStar : Entity<Guid>
{
    public Guid SkillListingId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SkillStar() : base(Guid.NewGuid()) { }

    public static SkillStar Create(Guid skillListingId, Guid userId)
    {
        return new SkillStar
        {
            SkillListingId = skillListingId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
