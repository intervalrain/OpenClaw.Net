using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Application.Updates;
using OpenClaw.Contracts.Configuration;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Updates.Controllers;

[ApiVersion("1.0")]
[Authorize(Roles = "SuperAdmin")]
public class UpdatesController(
    UpdateCheckerService updateChecker,
    IConfigStore configStore) : ApiController
{
    /// <summary>
    /// Get current update status + progress (available to all authenticated users).
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult GetStatus()
    {
        var currentVersion = UpdateCheckerService.CurrentVersion.ToString();
        var latestVersion = configStore.Get(ConfigKeys.LatestAvailableVersion);
        var updateStatus = configStore.Get(ConfigKeys.UpdateStatus) ?? "idle";
        var statusMessage = configStore.Get(ConfigKeys.UpdateStatusMessage);

        var updateAvailable = false;
        if (latestVersion is not null)
        {
            var latest = latestVersion.TrimStart('v', 'V');
            updateAvailable = Version.TryParse(latest, out var v) && v > UpdateCheckerService.CurrentVersion;
        }

        return Ok(new
        {
            currentVersion,
            latestVersion,
            updateAvailable,
            updateStatus,
            statusMessage
        });
    }

    /// <summary>
    /// Force an immediate update check (SuperAdmin only).
    /// </summary>
    [HttpPost("check")]
    public async Task<IActionResult> ForceCheck(CancellationToken ct)
    {
        var result = await updateChecker.CheckForUpdateAsync(ct);

        return Ok(new
        {
            result.UpdateAvailable,
            result.CurrentVersion,
            latestVersion = result.LatestRelease?.TagName,
            releaseName = result.LatestRelease?.Name,
            releaseNotes = result.LatestRelease?.Body,
            releaseUrl = result.LatestRelease?.HtmlUrl,
            publishedAt = result.LatestRelease?.PublishedAt
        });
    }

    /// <summary>
    /// Apply the update — pull new image and restart container (SuperAdmin only).
    /// Runs in background; poll GET /status for progress.
    /// </summary>
    [HttpPost("apply")]
    public async Task<IActionResult> ApplyUpdate(CancellationToken ct)
    {
        var latestVersion = configStore.Get(ConfigKeys.LatestAvailableVersion);
        if (latestVersion is null)
            return BadRequest("No update available. Run POST /check first.");

        var currentStatus = configStore.Get(ConfigKeys.UpdateStatus);
        if (currentStatus is "pulling" or "restarting")
            return Conflict("Update already in progress.");

        // Start update in background (fire-and-forget — will survive even if this request ends)
        _ = Task.Run(() => updateChecker.ApplyUpdateAsync(latestVersion));

        return Accepted(new { message = "Update started. Poll GET /status for progress." });
    }

    /// <summary>
    /// Get auto-update configuration (SuperAdmin only).
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            enabled = configStore.Get(ConfigKeys.AutoUpdateEnabled) ?? "true",
            checkIntervalHours = configStore.Get(ConfigKeys.AutoUpdateCheckInterval) ?? "24",
            repo = configStore.Get(ConfigKeys.AutoUpdateRepo) ?? "intervalrain/OpenClaw.Net"
        });
    }

    /// <summary>
    /// Update auto-update configuration (SuperAdmin only).
    /// </summary>
    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig([FromBody] UpdateConfigRequest req, CancellationToken ct)
    {
        if (req.Enabled is not null)
            await configStore.SetAsync(ConfigKeys.AutoUpdateEnabled, req.Enabled, ct: ct);
        if (req.CheckIntervalHours is not null)
            await configStore.SetAsync(ConfigKeys.AutoUpdateCheckInterval, req.CheckIntervalHours.ToString(), ct: ct);
        if (req.Repo is not null)
            await configStore.SetAsync(ConfigKeys.AutoUpdateRepo, req.Repo, ct: ct);

        return Ok(new { message = "Configuration updated" });
    }
}

public class UpdateConfigRequest
{
    public string? Enabled { get; init; }
    public int? CheckIntervalHours { get; init; }
    public string? Repo { get; init; }
}
