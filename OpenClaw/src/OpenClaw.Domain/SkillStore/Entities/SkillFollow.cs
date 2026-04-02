using Weda.Core.Domain;

namespace OpenClaw.Domain.SkillStore.Entities;

/// <summary>
/// Represents a user following a skill listing to receive update notifications.
/// </summary>
public class SkillFollow : Entity<Guid>
{
    public Guid SkillListingId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SkillFollow() : base(Guid.NewGuid()) { }

    public static SkillFollow Create(Guid skillListingId, Guid userId)
    {
        return new SkillFollow
        {
            SkillListingId = skillListingId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
