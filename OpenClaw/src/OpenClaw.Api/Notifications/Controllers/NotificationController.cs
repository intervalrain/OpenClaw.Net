using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Domain.Notifications.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Notifications.Controllers;

[ApiVersion("1.0")]
[Microsoft.AspNetCore.Authorization.Authorize]
public class NotificationController(
    INotificationRepository notificationRepository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork uow) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var userId = currentUserProvider.GetCurrentUser().Id;
        var notifications = await notificationRepository.GetByUserIdAsync(userId, unreadOnly, ct);

        return Ok(notifications.Select(n => new
        {
            n.Id,
            n.Title,
            n.Message,
            n.Type,
            n.Link,
            n.IsRead,
            n.CreatedAt
        }));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = currentUserProvider.GetCurrentUser().Id;
        var count = await notificationRepository.GetUnreadCountAsync(userId, ct);
        return Ok(new { count });
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var notification = await notificationRepository.GetByIdAsync(id, ct);
        if (notification is null)
            return NotFound();

        var userId = currentUserProvider.GetCurrentUser().Id;
        if (notification.UserId != userId)
            return Forbid();

        notification.MarkAsRead();
        await notificationRepository.UpdateAsync(notification, ct);
        await uow.SaveChangesAsync(ct);

        return Ok();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var userId = currentUserProvider.GetCurrentUser().Id;
        var unread = await notificationRepository.GetByUserIdAsync(userId, unreadOnly: true, ct);

        foreach (var n in unread)
            n.MarkAsRead();

        await uow.SaveChangesAsync(ct);
        return Ok(new { marked = unread.Count });
    }
}
