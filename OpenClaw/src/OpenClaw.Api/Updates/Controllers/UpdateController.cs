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
    /// Get current update status (current version, latest available, update available).
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous] // Allow all authenticated users to see version
    public IActionResult GetStatus()
    {
        var currentVersion = UpdateCheckerService.CurrentVersion.ToString();
        var latestVersion = configStore.Get(ConfigKeys.LatestAvailableVersion);

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
            updateAvailable
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
