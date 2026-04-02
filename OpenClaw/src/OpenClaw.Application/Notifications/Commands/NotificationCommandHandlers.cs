using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Notifications.Commands;
using OpenClaw.Domain.Notifications.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Notifications.Commands;

public class MarkNotificationReadCommandHandler(INotificationRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkNotificationReadCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await repository.GetByIdAsync(request.NotificationId, ct);
        if (notification is null)
            return Error.NotFound("Notification not found.");

        if (notification.UserId != request.UserId)
            return Error.Unauthorized(description: "Cannot mark another user's notification as read.");

        notification.MarkAsRead();
        await repository.UpdateAsync(notification, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class MarkAllNotificationsReadCommandHandler(INotificationRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkAllNotificationsReadCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        await repository.MarkAllAsReadAsync(request.UserId, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}
