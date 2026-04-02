using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.SkillStore.Commands;
using OpenClaw.Contracts.SkillStore.Queries;
using OpenClaw.Contracts.SkillStore.Requests;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.SkillStore.Controllers;

/// <summary>
/// Skill Store — enterprise internal skill marketplace.
/// Users publish skills, admins review, others install/star/follow.
/// </summary>
[ApiVersion("1.0")]
public class SkillStoreController(ISender sender, ICurrentUserProvider currentUserProvider) : ApiController
{
    private Guid GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return Guid.Empty; }
    }

    private string GetUserName()
    {
        try { return currentUserProvider.GetCurrentUser().Name; }
        catch { return string.Empty; }
    }

    // === Browse ===

    /// <summary>
    /// Browse approved skills in the store with optional filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var result = await sender.Send(new GetSkillListingsQuery(status, category, search, limit, offset, userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Get detailed info about a specific skill listing.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await sender.Send(new GetSkillListingQuery(id, userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    // === Author Operations ===

    /// <summary>
    /// Publish a new skill to the store (goes to PendingReview).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Publish([FromBody] PublishSkillRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var userName = GetUserName();

        var result = await sender.Send(new PublishSkillCommand(
            request.Name, request.DisplayName, request.Description, request.Version,
            request.ContentJson, userId, userName, request.IconUrl, request.RepositoryUrl,
            request.Category, request.Tags), ct);

        return result.Match<IActionResult>(
            skill => CreatedAtAction(nameof(Get), new { id = skill.Id }, skill),
            errors => Problem(errors));
    }

    /// <summary>
    /// Update metadata for a skill listing (author only).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSkillListingRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UpdateSkillListingCommand(
            id, userId, request.DisplayName, request.Description, request.IconUrl,
            request.RepositoryUrl, request.Category, request.Tags), ct);

        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Publish a new version (triggers re-review and notifies followers/installers).
    /// </summary>
    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> PublishNewVersion(Guid id, [FromBody] PublishNewVersionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new PublishNewVersionCommand(id, userId, request.Version, request.ContentJson), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Delete a skill listing (author only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new DeleteSkillListingCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Get skills published by the current user.
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMySkills(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetMySkillListingsQuery(userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    // === Admin Review ===

    /// <summary>
    /// Get all skills pending review (admin/super admin only).
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policy = Policy.AdminOrAbove)]
    public async Task<IActionResult> GetPendingReviews(CancellationToken ct)
    {
        var result = await sender.Send(new GetPendingSkillReviewsQuery(), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Review a skill (approve/reject) — admin/super admin only.
    /// </summary>
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = Policy.AdminOrAbove)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewSkillRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var userName = GetUserName();

        var result = await sender.Send(new ReviewSkillCommand(id, userId, userName, request.Decision, request.Comment), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    // === Star / Follow / Install ===

    /// <summary>
    /// Star a skill.
    /// </summary>
    [HttpPost("{id:guid}/star")]
    public async Task<IActionResult> Star(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new StarSkillCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Unstar a skill.
    /// </summary>
    [HttpDelete("{id:guid}/star")]
    public async Task<IActionResult> Unstar(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UnstarSkillCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Follow a skill to receive update notifications.
    /// </summary>
    [HttpPost("{id:guid}/follow")]
    public async Task<IActionResult> Follow(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new FollowSkillCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Unfollow a skill.
    /// </summary>
    [HttpDelete("{id:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UnfollowSkillCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Install a skill.
    /// </summary>
    [HttpPost("{id:guid}/install")]
    public async Task<IActionResult> Install(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new InstallSkillCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Uninstall a skill.
    /// </summary>
    [HttpDelete("{id:guid}/install")]
    public async Task<IActionResult> Uninstall(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UninstallSkillCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Upgrade an installed skill to the latest version.
    /// </summary>
    [HttpPost("{id:guid}/upgrade")]
    public async Task<IActionResult> Upgrade(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UpgradeSkillCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Get skills installed by the current user.
    /// </summary>
    [HttpGet("installed")]
    public async Task<IActionResult> GetInstalled(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetInstalledSkillsQuery(userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Get skills starred by the current user.
    /// </summary>
    [HttpGet("starred")]
    public async Task<IActionResult> GetStarred(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetStarredSkillsQuery(userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }
}
