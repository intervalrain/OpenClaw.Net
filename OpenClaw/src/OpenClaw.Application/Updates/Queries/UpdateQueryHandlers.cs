using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Updates.Queries;
using OpenClaw.Contracts.Updates.Responses;
using OpenClaw.Domain.Updates.Repositories;

namespace OpenClaw.Application.Updates.Queries;

public class GetPendingUpdatesQueryHandler(ISystemUpdateRepository repository)
    : IRequestHandler<GetPendingUpdatesQuery, ErrorOr<IReadOnlyList<SystemUpdateResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<SystemUpdateResponse>>> Handle(
        GetPendingUpdatesQuery request, CancellationToken ct)
    {
        var updates = await repository.GetPendingAsync(ct);
        return updates.Select(ToResponse).ToList();
    }

    private static SystemUpdateResponse ToResponse(Domain.Updates.Entities.SystemUpdate u) => new()
    {
        Id = u.Id,
        TagName = u.TagName,
        ReleaseName = u.ReleaseName,
        ReleaseNotes = u.ReleaseNotes,
        HtmlUrl = u.HtmlUrl,
        PublishedAt = u.PublishedAt,
        IsAcknowledged = u.IsAcknowledged,
        IsDismissed = u.IsDismissed,
        DetectedAt = u.DetectedAt,
    };
}

public class GetAllUpdatesQueryHandler(ISystemUpdateRepository repository)
    : IRequestHandler<GetAllUpdatesQuery, ErrorOr<IReadOnlyList<SystemUpdateResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<SystemUpdateResponse>>> Handle(
        GetAllUpdatesQuery request, CancellationToken ct)
    {
        var updates = await repository.GetAllAsync(ct);
        return updates.Select(u => new SystemUpdateResponse
        {
            Id = u.Id,
            TagName = u.TagName,
            ReleaseName = u.ReleaseName,
            ReleaseNotes = u.ReleaseNotes,
            HtmlUrl = u.HtmlUrl,
            PublishedAt = u.PublishedAt,
            IsAcknowledged = u.IsAcknowledged,
            IsDismissed = u.IsDismissed,
            DetectedAt = u.DetectedAt,
        }).ToList();
    }
}
