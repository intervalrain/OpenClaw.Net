using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Notifications.Responses;

namespace OpenClaw.Contracts.Notifications.Queries;

public record GetNotificationsQuery(Guid UserId, bool UnreadOnly = false, int Limit = 50, int Offset = 0)
    : IRequest<ErrorOr<IReadOnlyList<NotificationResponse>>>;

public record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<ErrorOr<int>>;
