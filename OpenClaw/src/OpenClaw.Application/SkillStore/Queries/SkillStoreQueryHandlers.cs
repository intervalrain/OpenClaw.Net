using ErrorOr;
using Mediator;
using OpenClaw.Contracts.SkillStore.Queries;
using OpenClaw.Contracts.SkillStore.Responses;
using OpenClaw.Domain.SkillStore.Entities;
using OpenClaw.Domain.SkillStore.Enums;
using OpenClaw.Domain.SkillStore.Repositories;

namespace OpenClaw.Application.SkillStore.Queries;

public class GetSkillListingsQueryHandler(ISkillListingRepository listingRepository)
    : IRequestHandler<GetSkillListingsQuery, ErrorOr<IReadOnlyList<SkillListingSummaryResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<SkillListingSummaryResponse>>> Handle(
        GetSkillListingsQuery request, CancellationToken ct)
    {
        SkillPublishStatus? status = null;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<SkillPublishStatus>(request.Status, true, out var parsed))
            status = parsed;

        var listings = await listingRepository.GetAllAsync(
            status ?? SkillPublishStatus.Approved,
            request.Category,
            request.Search,
            request.Limit,
            request.Offset,
            ct);

        return listings.Select(l => new SkillListingSummaryResponse
        {
            Id = l.Id,
            Name = l.Name,
            DisplayName = l.DisplayName,
            Description = l.Description,
            Version = l.Version,
            IconUrl = l.IconUrl,
            Category = l.Category,
            AuthorName = l.AuthorName,
            StarCount = l.StarCount,
            DownloadCount = l.DownloadCount,
        }).ToList();
    }
}

public class GetSkillListingQueryHandler(
    ISkillListingRepository listingRepository,
    ISkillStarRepository starRepository,
    ISkillFollowRepository followRepository,
    ISkillInstallationRepository installationRepository)
    : IRequestHandler<GetSkillListingQuery, ErrorOr<SkillListingResponse>>
{
    public async ValueTask<ErrorOr<SkillListingResponse>> Handle(
        GetSkillListingQuery request, CancellationToken ct)
    {
        var listing = await listingRepository.GetByIdAsync(request.Id, ct);
        if (listing is null)
            return Error.NotFound("Skill listing not found.");

        bool isStarred = false, isFollowed = false, isInstalled = false;
        string? installedVersion = null;
        bool hasUpdate = false;

        if (request.CurrentUserId.HasValue)
        {
            var star = await starRepository.GetAsync(listing.Id, request.CurrentUserId.Value, ct);
            isStarred = star is not null;

            var follow = await followRepository.GetAsync(listing.Id, request.CurrentUserId.Value, ct);
            isFollowed = follow is not null;

            var installation = await installationRepository.GetAsync(listing.Id, request.CurrentUserId.Value, ct);
            if (installation is not null)
            {
                isInstalled = true;
                installedVersion = installation.InstalledVersion;
                hasUpdate = installation.HasUpdate;
            }
        }

        return ToDetailResponse(listing, isStarred, isFollowed, isInstalled, installedVersion, hasUpdate);
    }

    internal static SkillListingResponse ToDetailResponse(
        SkillListing l,
        bool isStarred = false,
        bool isFollowed = false,
        bool isInstalled = false,
        string? installedVersion = null,
        bool hasUpdate = false) => new()
    {
        Id = l.Id,
        Name = l.Name,
        DisplayName = l.DisplayName,
        Description = l.Description,
        Version = l.Version,
        IconUrl = l.IconUrl,
        RepositoryUrl = l.RepositoryUrl,
        Category = l.Category,
        Tags = l.Tags,
        Status = l.Status.ToString(),
        ReviewComment = l.ReviewComment,
        AuthorUserId = l.AuthorUserId,
        AuthorName = l.AuthorName,
        StarCount = l.StarCount,
        FollowerCount = l.FollowerCount,
        DownloadCount = l.DownloadCount,
        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt,
        IsStarred = isStarred,
        IsFollowed = isFollowed,
        IsInstalled = isInstalled,
        InstalledVersion = installedVersion,
        HasUpdate = hasUpdate,
    };
}

public class GetMySkillListingsQueryHandler(ISkillListingRepository repository)
    : IRequestHandler<GetMySkillListingsQuery, ErrorOr<IReadOnlyList<SkillListingResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<SkillListingResponse>>> Handle(
        GetMySkillListingsQuery request, CancellationToken ct)
    {
        var listings = await repository.GetByAuthorAsync(request.UserId, ct);
        return listings.Select(l => GetSkillListingQueryHandler.ToDetailResponse(l)).ToList();
    }
}

public class GetPendingSkillReviewsQueryHandler(ISkillListingRepository repository)
    : IRequestHandler<GetPendingSkillReviewsQuery, ErrorOr<IReadOnlyList<SkillListingResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<SkillListingResponse>>> Handle(
        GetPendingSkillReviewsQuery request, CancellationToken ct)
    {
        var listings = await repository.GetPendingReviewAsync(ct);
        return listings.Select(l => GetSkillListingQueryHandler.ToDetailResponse(l)).ToList();
    }
}

public class GetInstalledSkillsQueryHandler(
    ISkillInstallationRepository installationRepository,
    ISkillListingRepository listingRepository)
    : IRequestHandler<GetInstalledSkillsQuery, ErrorOr<IReadOnlyList<SkillListingResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<SkillListingResponse>>> Handle(
        GetInstalledSkillsQuery request, CancellationToken ct)
    {
        var installations = await installationRepository.GetByUserAsync(request.UserId, ct);
        var results = new List<SkillListingResponse>();

        foreach (var inst in installations)
        {
            var listing = await listingRepository.GetByIdAsync(inst.SkillListingId, ct);
            if (listing is not null)
            {
                results.Add(GetSkillListingQueryHandler.ToDetailResponse(
                    listing, isInstalled: true, installedVersion: inst.InstalledVersion, hasUpdate: inst.HasUpdate));
            }
        }

        return results;
    }
}

public class GetStarredSkillsQueryHandler(
    ISkillStarRepository starRepository,
    ISkillListingRepository listingRepository)
    : IRequestHandler<GetStarredSkillsQuery, ErrorOr<IReadOnlyList<SkillListingSummaryResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<SkillListingSummaryResponse>>> Handle(
        GetStarredSkillsQuery request, CancellationToken ct)
    {
        var stars = await starRepository.GetByUserAsync(request.UserId, ct);
        var results = new List<SkillListingSummaryResponse>();

        foreach (var star in stars)
        {
            var listing = await listingRepository.GetByIdAsync(star.SkillListingId, ct);
            if (listing is not null)
            {
                results.Add(new SkillListingSummaryResponse
                {
                    Id = listing.Id,
                    Name = listing.Name,
                    DisplayName = listing.DisplayName,
                    Description = listing.Description,
                    Version = listing.Version,
                    IconUrl = listing.IconUrl,
                    Category = listing.Category,
                    AuthorName = listing.AuthorName,
                    StarCount = listing.StarCount,
                    DownloadCount = listing.DownloadCount,
                });
            }
        }

        return results;
    }
}
