using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Notifications.Commands;
using OpenClaw.Contracts.Notifications.Queries;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Notifications.Controllers;

/// <summary>
/// In-app notifications for the current user.
/// </summary>
[ApiVersion("1.0")]
public class NotificationController(ISender sender, ICurrentUserProvider currentUserProvider) : ApiController
{
    private Guid GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return Guid.Empty; }
    }

    /// <summary>
    /// Get notifications for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetNotificationsQuery(userId, unreadOnly, limit, offset), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Get unread notification count.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetUnreadNotificationCountQuery(userId), ct);
        return result.Match<IActionResult>(count => Ok(new { count }), errors => Problem(errors));
    }

    /// <summary>
    /// Mark a specific notification as read.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new MarkNotificationReadCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Mark all notifications as read.
    /// </summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new MarkAllNotificationsReadCommand(userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }
}
