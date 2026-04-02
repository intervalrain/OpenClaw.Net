namespace OpenClaw.Domain.SkillStore.Enums;

public enum SkillPublishStatus
{
    Draft,
    PendingReview,
    Approved,
    Rejected,
    Deprecated,
}

public enum SkillReviewDecision
{
    Approved,
    Rejected,
    RequestChanges,
}
