using ErrorOr;
using Mediator;
using OpenClaw.Contracts.SkillStore.Responses;

namespace OpenClaw.Contracts.SkillStore.Queries;

public record GetSkillListingsQuery(
    string? Status = null,
    string? Category = null,
    string? Search = null,
    int Limit = 50,
    int Offset = 0,
    Guid? CurrentUserId = null) : IRequest<ErrorOr<IReadOnlyList<SkillListingSummaryResponse>>>;

public record GetSkillListingQuery(Guid Id, Guid? CurrentUserId = null) : IRequest<ErrorOr<SkillListingResponse>>;

public record GetMySkillListingsQuery(Guid UserId) : IRequest<ErrorOr<IReadOnlyList<SkillListingResponse>>>;

public record GetPendingSkillReviewsQuery : IRequest<ErrorOr<IReadOnlyList<SkillListingResponse>>>;

public record GetInstalledSkillsQuery(Guid UserId) : IRequest<ErrorOr<IReadOnlyList<SkillListingResponse>>>;

public record GetStarredSkillsQuery(Guid UserId) : IRequest<ErrorOr<IReadOnlyList<SkillListingSummaryResponse>>>;
