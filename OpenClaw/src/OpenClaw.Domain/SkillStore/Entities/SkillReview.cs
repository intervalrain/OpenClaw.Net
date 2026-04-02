using OpenClaw.Domain.SkillStore.Enums;
using Weda.Core.Domain;

namespace OpenClaw.Domain.SkillStore.Entities;

/// <summary>
/// Admin review record for a skill listing submission.
/// </summary>
public class SkillReview : Entity<Guid>
{
    public Guid SkillListingId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public string ReviewerName { get; private set; } = null!;
    public SkillReviewDecision Decision { get; private set; }
    public string? Comment { get; private set; }
    public string VersionReviewed { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private SkillReview() : base(Guid.NewGuid()) { }

    public static SkillReview Create(
        Guid skillListingId,
        Guid reviewerUserId,
        string reviewerName,
        SkillReviewDecision decision,
        string versionReviewed,
        string? comment = null)
    {
        return new SkillReview
        {
            SkillListingId = skillListingId,
            ReviewerUserId = reviewerUserId,
            ReviewerName = reviewerName,
            Decision = decision,
            Comment = comment,
            VersionReviewed = versionReviewed,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
