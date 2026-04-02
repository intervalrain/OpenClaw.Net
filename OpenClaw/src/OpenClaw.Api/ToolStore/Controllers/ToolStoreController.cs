using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.ToolStore.Commands;
using OpenClaw.Contracts.ToolStore.Queries;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.ToolStore.Controllers;

/// <summary>
/// Tool Store — official tool packages managed by super admin.
/// </summary>
[ApiVersion("1.0")]
[Authorize(Policy = Policy.SuperAdminOnly)]
public class ToolStoreController(ISender sender, ICurrentUserProvider currentUserProvider) : ApiController
{
    private Guid GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return Guid.Empty; }
    }

    /// <summary>
    /// List all tool packages in the store.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? installedOnly, CancellationToken ct)
    {
        var result = await sender.Send(new GetToolPackagesQuery(installedOnly), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Get a specific tool package.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetToolPackageQuery(id), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Install a tool package.
    /// </summary>
    [HttpPost("{id:guid}/install")]
    public async Task<IActionResult> Install(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new InstallToolPackageCommand(id, userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    /// <summary>
    /// Uninstall a tool package.
    /// </summary>
    [HttpPost("{id:guid}/uninstall")]
    public async Task<IActionResult> Uninstall(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UninstallToolPackageCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Upgrade a tool package to the latest version.
    /// </summary>
    [HttpPost("{id:guid}/upgrade")]
    public async Task<IActionResult> Upgrade(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UpgradeToolPackageCommand(id, userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }
}
