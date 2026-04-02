using ErrorOr;
using Mediator;

namespace OpenClaw.Contracts.Notifications.Commands;

public record MarkNotificationReadCommand(Guid NotificationId, Guid UserId) : IRequest<ErrorOr<Success>>;

public record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<ErrorOr<Success>>;
