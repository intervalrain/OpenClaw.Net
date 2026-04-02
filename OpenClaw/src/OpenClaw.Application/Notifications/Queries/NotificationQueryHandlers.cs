using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Notifications.Queries;
using OpenClaw.Contracts.Notifications.Responses;
using OpenClaw.Domain.Notifications.Repositories;

namespace OpenClaw.Application.Notifications.Queries;

public class GetNotificationsQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetNotificationsQuery, ErrorOr<IReadOnlyList<NotificationResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<NotificationResponse>>> Handle(
        GetNotificationsQuery request, CancellationToken ct)
    {
        var notifications = await repository.GetByUserAsync(
            request.UserId, request.UnreadOnly, request.Limit, request.Offset, ct);

        return notifications.Select(n => new NotificationResponse
        {
            Id = n.Id,
            Type = n.Type.ToString(),
            Title = n.Title,
            Message = n.Message,
            ReferenceId = n.ReferenceId,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            ReadAt = n.ReadAt,
        }).ToList();
    }
}

public class GetUnreadNotificationCountQueryHandler(INotificationRepository repository)
    : IRequestHandler<GetUnreadNotificationCountQuery, ErrorOr<int>>
{
    public async ValueTask<ErrorOr<int>> Handle(
        GetUnreadNotificationCountQuery request, CancellationToken ct)
    {
        return await repository.GetUnreadCountAsync(request.UserId, ct);
    }
}
