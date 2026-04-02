using ErrorOr;
using Mediator;
using OpenClaw.Contracts.SkillStore.Responses;

namespace OpenClaw.Contracts.SkillStore.Commands;

public record PublishSkillCommand(
    string Name,
    string DisplayName,
    string? Description,
    string Version,
    string ContentJson,
    Guid AuthorUserId,
    string AuthorName,
    string? IconUrl,
    string? RepositoryUrl,
    string? Category,
    string? Tags) : IRequest<ErrorOr<SkillListingResponse>>;

public record UpdateSkillListingCommand(
    Guid Id,
    Guid UserId,
    string? DisplayName,
    string? Description,
    string? IconUrl,
    string? RepositoryUrl,
    string? Category,
    string? Tags) : IRequest<ErrorOr<SkillListingResponse>>;

public record PublishNewVersionCommand(
    Guid SkillListingId,
    Guid UserId,
    string Version,
    string ContentJson) : IRequest<ErrorOr<SkillListingResponse>>;

public record ReviewSkillCommand(
    Guid SkillListingId,
    Guid ReviewerUserId,
    string ReviewerName,
    string Decision,
    string? Comment) : IRequest<ErrorOr<SkillListingResponse>>;

public record StarSkillCommand(Guid SkillListingId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record UnstarSkillCommand(Guid SkillListingId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record FollowSkillCommand(Guid SkillListingId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record UnfollowSkillCommand(Guid SkillListingId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record InstallSkillCommand(Guid SkillListingId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record UninstallSkillCommand(Guid SkillListingId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record UpgradeSkillCommand(Guid SkillListingId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record DeleteSkillListingCommand(Guid Id, Guid UserId) : IRequest<ErrorOr<Success>>;
