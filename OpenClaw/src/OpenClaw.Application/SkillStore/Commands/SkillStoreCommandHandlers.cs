using ErrorOr;
using Mediator;
using OpenClaw.Application.SkillStore.Queries;
using OpenClaw.Contracts.SkillStore.Commands;
using OpenClaw.Contracts.SkillStore.Responses;
using OpenClaw.Domain.SkillStore.Entities;
using OpenClaw.Domain.SkillStore.Enums;
using OpenClaw.Domain.SkillStore.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.SkillStore.Commands;

public class PublishSkillCommandHandler(ISkillListingRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<PublishSkillCommand, ErrorOr<SkillListingResponse>>
{
    public async ValueTask<ErrorOr<SkillListingResponse>> Handle(PublishSkillCommand request, CancellationToken ct)
    {
        var existing = await repository.GetByNameAsync(request.Name.ToLowerInvariant().Trim(), ct);
        if (existing is not null)
            return Error.Conflict($"A skill with name '{request.Name}' already exists.");

        var listing = SkillListing.Create(
            request.Name,
            request.DisplayName,
            request.Description,
            request.Version,
            request.ContentJson,
            request.AuthorUserId,
            request.AuthorName,
            request.IconUrl,
            request.RepositoryUrl,
            request.Category,
            request.Tags);

        await repository.AddAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return GetSkillListingQueryHandler.ToDetailResponse(listing);
    }
}

public class UpdateSkillListingCommandHandler(ISkillListingRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateSkillListingCommand, ErrorOr<SkillListingResponse>>
{
    public async ValueTask<ErrorOr<SkillListingResponse>> Handle(UpdateSkillListingCommand request, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(request.Id, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        if (listing.AuthorUserId != request.UserId)
            return Error.Unauthorized(description: "Only the author can update this skill listing.");

        listing.Update(request.DisplayName, request.Description, request.IconUrl,
            request.RepositoryUrl, request.Category, request.Tags);

        await repository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return GetSkillListingQueryHandler.ToDetailResponse(listing);
    }
}

public class PublishNewVersionCommandHandler(
    ISkillListingRepository repository,
    ISkillInstallationRepository installationRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<PublishNewVersionCommand, ErrorOr<SkillListingResponse>>
{
    public async ValueTask<ErrorOr<SkillListingResponse>> Handle(PublishNewVersionCommand request, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(request.SkillListingId, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        if (listing.AuthorUserId != request.UserId)
            return Error.Unauthorized(description: "Only the author can publish new versions.");

        listing.PublishNewVersion(request.Version, request.ContentJson);

        await repository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return GetSkillListingQueryHandler.ToDetailResponse(listing);
    }
}

public class ReviewSkillCommandHandler(ISkillListingRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<ReviewSkillCommand, ErrorOr<SkillListingResponse>>
{
    public async ValueTask<ErrorOr<SkillListingResponse>> Handle(ReviewSkillCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<SkillReviewDecision>(request.Decision, true, out var decision))
            return Error.Validation($"Invalid review decision '{request.Decision}'.");

        var listing = await repository.GetByIdAsync(request.SkillListingId, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        if (listing.Status != SkillPublishStatus.PendingReview)
            return Error.Validation("This skill is not pending review.");

        switch (decision)
        {
            case SkillReviewDecision.Approved:
                listing.Approve(request.ReviewerUserId, request.Comment);
                break;
            case SkillReviewDecision.Rejected:
            case SkillReviewDecision.RequestChanges:
                listing.Reject(request.ReviewerUserId, request.Comment);
                break;
        }

        await repository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return GetSkillListingQueryHandler.ToDetailResponse(listing);
    }
}

public class StarSkillCommandHandler(
    ISkillListingRepository listingRepository,
    ISkillStarRepository starRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<StarSkillCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(StarSkillCommand request, CancellationToken ct)
    {
        var listing = await listingRepository.GetByIdAsync(request.SkillListingId, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        var existing = await starRepository.GetAsync(request.SkillListingId, request.UserId, ct);
        if (existing is not null)
            return Error.Conflict("Already starred.");

        var star = SkillStar.Create(request.SkillListingId, request.UserId);
        listing.IncrementStarCount();

        await starRepository.AddAsync(star, ct);
        await listingRepository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class UnstarSkillCommandHandler(
    ISkillListingRepository listingRepository,
    ISkillStarRepository starRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnstarSkillCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UnstarSkillCommand request, CancellationToken ct)
    {
        var existing = await starRepository.GetAsync(request.SkillListingId, request.UserId, ct);
        if (existing is null)
            return Error.NotFound("Not starred.");

        var listing = await listingRepository.GetByIdAsync(request.SkillListingId, ct);
        listing?.DecrementStarCount();

        await starRepository.DeleteAsync(existing, ct);
        if (listing is not null)
            await listingRepository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class FollowSkillCommandHandler(
    ISkillListingRepository listingRepository,
    ISkillFollowRepository followRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<FollowSkillCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(FollowSkillCommand request, CancellationToken ct)
    {
        var listing = await listingRepository.GetByIdAsync(request.SkillListingId, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        var existing = await followRepository.GetAsync(request.SkillListingId, request.UserId, ct);
        if (existing is not null)
            return Error.Conflict("Already following.");

        var follow = SkillFollow.Create(request.SkillListingId, request.UserId);
        listing.IncrementFollowerCount();

        await followRepository.AddAsync(follow, ct);
        await listingRepository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class UnfollowSkillCommandHandler(
    ISkillListingRepository listingRepository,
    ISkillFollowRepository followRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnfollowSkillCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UnfollowSkillCommand request, CancellationToken ct)
    {
        var existing = await followRepository.GetAsync(request.SkillListingId, request.UserId, ct);
        if (existing is null)
            return Error.NotFound("Not following.");

        var listing = await listingRepository.GetByIdAsync(request.SkillListingId, ct);
        listing?.DecrementFollowerCount();

        await followRepository.DeleteAsync(existing, ct);
        if (listing is not null)
            await listingRepository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class InstallSkillCommandHandler(
    ISkillListingRepository listingRepository,
    ISkillInstallationRepository installationRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<InstallSkillCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(InstallSkillCommand request, CancellationToken ct)
    {
        var listing = await listingRepository.GetByIdAsync(request.SkillListingId, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        if (listing.Status != SkillPublishStatus.Approved)
            return Error.Validation("Can only install approved skills.");

        var existing = await installationRepository.GetAsync(request.SkillListingId, request.UserId, ct);
        if (existing is not null)
            return Error.Conflict("Skill is already installed.");

        var installation = SkillInstallation.Create(request.SkillListingId, request.UserId, listing.Version);
        listing.IncrementDownloadCount();

        await installationRepository.AddAsync(installation, ct);
        await listingRepository.UpdateAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class UninstallSkillCommandHandler(
    ISkillInstallationRepository installationRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UninstallSkillCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UninstallSkillCommand request, CancellationToken ct)
    {
        var installation = await installationRepository.GetAsync(request.SkillListingId, request.UserId, ct);
        if (installation is null)
            return Error.NotFound("Skill is not installed.");

        await installationRepository.DeleteAsync(installation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class UpgradeSkillCommandHandler(
    ISkillListingRepository listingRepository,
    ISkillInstallationRepository installationRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpgradeSkillCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpgradeSkillCommand request, CancellationToken ct)
    {
        var listing = await listingRepository.GetByIdAsync(request.SkillListingId, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        var installation = await installationRepository.GetAsync(request.SkillListingId, request.UserId, ct);
        if (installation is null)
            return Error.NotFound("Skill is not installed.");

        installation.UpgradeTo(listing.Version);
        await installationRepository.UpdateAsync(installation, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class DeleteSkillListingCommandHandler(ISkillListingRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteSkillListingCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteSkillListingCommand request, CancellationToken ct)
    {
        var listing = await repository.GetByIdAsync(request.Id, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        if (listing.AuthorUserId != request.UserId)
            return Error.Unauthorized(description: "Only the author can delete this skill listing.");

        await repository.DeleteAsync(listing, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}
