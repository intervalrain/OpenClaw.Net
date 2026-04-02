using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Updates.Commands;
using OpenClaw.Contracts.Updates.Queries;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Updates.Controllers;

/// <summary>
/// System update management — detects GitHub releases and lets super admin acknowledge/dismiss.
/// </summary>
[ApiVersion("1.0")]
[Authorize(Policy = Policy.SuperAdminOnly)]
public class SystemUpdateController(ISender sender, ICurrentUserProvider currentUserProvider) : ApiController
{
    private Guid GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return Guid.Empty; }
    }

    /// <summary>
    /// Get all pending (unacknowledged, undismissed) updates.
    /// </summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var result = await sender.Send(new GetPendingUpdatesQuery(), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Get all detected updates (including acknowledged/dismissed).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await sender.Send(new GetAllUpdatesQuery(), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Acknowledge an update (marks as reviewed, intent to update).
    /// </summary>
    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new AcknowledgeUpdateCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Dismiss an update (skip this version).
    /// </summary>
    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new DismissUpdateCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }
}
