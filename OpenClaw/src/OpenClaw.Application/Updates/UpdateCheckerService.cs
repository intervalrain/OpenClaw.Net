using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Domain.Notifications.Entities;
using OpenClaw.Domain.Notifications.Repositories;
using OpenClaw.Domain.Users.Repositories;
using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security.Models;

namespace OpenClaw.Application.Updates;

/// <summary>
/// Background service that periodically checks GitHub releases for newer versions
/// and notifies super-admins when an update is available.
/// </summary>
public class UpdateCheckerService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<UpdateCheckerService> logger) : Microsoft.Extensions.Hosting.BackgroundService
{
    private const string DefaultRepo = "intervalrain/OpenClaw.Net";
    private const int DefaultCheckIntervalHours = 24;

    public static Version CurrentVersion { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check if we just finished an update (container was restarted)
        await CheckPostUpdateAsync();

        // Wait 2 minutes after startup before first check
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdateAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Update check failed");
            }

            var intervalHours = await GetCheckIntervalAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
        }
    }

    /// <summary>
    /// Public entry point for manual check via API.
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();

        // Check if auto-update is enabled
        var enabled = configStore.Get(ConfigKeys.AutoUpdateEnabled);
        if (enabled?.Equals("false", StringComparison.OrdinalIgnoreCase) == true)
        {
            logger.LogDebug("Auto-update check skipped (disabled)");
            return new UpdateCheckResult(false, CurrentVersion.ToString(), null);
        }

        var repo = configStore.Get(ConfigKeys.AutoUpdateRepo) ?? DefaultRepo;
        var release = await FetchLatestReleaseAsync(repo, ct);

        if (release is null)
            return new UpdateCheckResult(false, CurrentVersion.ToString(), null);

        var latestVersion = release.GetVersion();
        if (latestVersion is null)
        {
            logger.LogWarning("Could not parse version from tag: {Tag}", release.TagName);
            return new UpdateCheckResult(false, CurrentVersion.ToString(), null);
        }

        var updateAvailable = latestVersion > CurrentVersion;
        logger.LogInformation("Update check: current={Current}, latest={Latest}, available={Available}",
            CurrentVersion, latestVersion, updateAvailable);

        if (updateAvailable)
        {
            // Store latest version in config
            await configStore.SetAsync(ConfigKeys.LatestAvailableVersion, release.TagName, ct: ct);

            // Check if we already notified for this version
            var lastNotified = configStore.Get(ConfigKeys.LastNotifiedVersion);
            if (lastNotified != release.TagName)
            {
                await NotifySuperAdminsAsync(scope.ServiceProvider, release, ct);
                await configStore.SetAsync(ConfigKeys.LastNotifiedVersion, release.TagName, ct: ct);
            }
        }

        return new UpdateCheckResult(updateAvailable, CurrentVersion.ToString(), release);
    }

    private async Task<GitHubReleaseInfo?> FetchLatestReleaseAsync(string repo, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "OpenClaw.NET-UpdateChecker");
            client.Timeout = TimeSpan.FromSeconds(10);

            var url = $"https://api.github.com/repos/{repo}/releases/latest";
            var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GitHub API returned {Status} for {Url}", response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GitHubReleaseInfo>(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch latest release from GitHub");
            return null;
        }
    }

    private async Task NotifySuperAdminsAsync(IServiceProvider sp, GitHubReleaseInfo release, CancellationToken ct)
    {
        var userRepo = sp.GetRequiredService<IUserRepository>();
        var notificationRepo = sp.GetRequiredService<INotificationRepository>();
        var uow = sp.GetRequiredService<IUnitOfWork>();

        var allUsers = await userRepo.GetAllAsync(ct);
        var superAdmins = allUsers.Where(u => u.Roles.Contains(Role.SuperAdmin)).ToList();

        if (superAdmins.Count == 0)
        {
            logger.LogWarning("No super-admins found to notify about update {Version}", release.TagName);
            return;
        }

        var title = $"Update Available: {release.TagName}";
        var message = $"{release.Name ?? release.TagName} is available.\n\n" +
                      $"{TruncateReleaseNotes(release.Body, 300)}";

        foreach (var admin in superAdmins)
        {
            var notification = Notification.Create(
                admin.Id,
                title,
                message,
                type: "update",
                link: release.HtmlUrl);

            await notificationRepo.AddAsync(notification, ct);
        }

        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Notified {Count} super-admin(s) about update {Version}",
            superAdmins.Count, release.TagName);
    }

    private async Task<int> GetCheckIntervalAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();
            var interval = configStore.Get(ConfigKeys.AutoUpdateCheckInterval);
            return int.TryParse(interval, out var hours) ? hours : DefaultCheckIntervalHours;
        }
        catch
        {
            return DefaultCheckIntervalHours;
        }
    }

    /// <summary>
    /// Applies the update: pulls new Docker image and restarts the container.
    /// Progress is written to IConfigStore so all clients can poll status.
    /// </summary>
    public async Task ApplyUpdateAsync(string targetVersion)
    {
        using var scope = scopeFactory.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();

        try
        {
            await SetStatusAsync(configStore, "pulling", $"Pulling latest image for {targetVersion}...");

            // docker compose pull api
            var pullResult = await RunDockerCommandAsync("docker compose pull api");
            if (pullResult.ExitCode != 0)
            {
                await SetStatusAsync(configStore, "failed", $"Pull failed: {pullResult.Error}");
                return;
            }

            await SetStatusAsync(configStore, "restarting", "Restarting container with new image...");

            // docker compose up -d --no-deps api (recreates only the api container)
            _ = RunDockerCommandAsync("docker compose up -d --no-deps api");

            // Note: this process will be killed when the container restarts.
            // The new container will read UPDATE_STATUS=restarting from DB.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Update apply failed");
            try { await SetStatusAsync(configStore, "failed", ex.Message); } catch { }
        }
    }

    /// <summary>
    /// Called on startup to check if we just finished an update.
    /// </summary>
    public async Task CheckPostUpdateAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();

        var status = configStore.Get(ConfigKeys.UpdateStatus);
        if (status is "restarting")
        {
            await SetStatusAsync(configStore, "completed",
                $"Successfully updated to {CurrentVersion}");
            logger.LogInformation("Post-update: marked update as completed (v{Version})", CurrentVersion);
        }
    }

    private static async Task SetStatusAsync(IConfigStore configStore, string status, string message)
    {
        await configStore.SetAsync(ConfigKeys.UpdateStatus, status);
        await configStore.SetAsync(ConfigKeys.UpdateStatusMessage, message);
    }

    private static async Task<DockerResult> RunDockerCommandAsync(string command)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"{command}\"",
            WorkingDirectory = "/app/project",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new DockerResult(process.ExitCode, output, error);
    }

    private static string TruncateReleaseNotes(string? body, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(body)) return "No release notes.";
        return body.Length <= maxLength ? body : body[..maxLength] + "...";
    }
}

public record UpdateCheckResult(bool UpdateAvailable, string CurrentVersion, GitHubReleaseInfo? LatestRelease);
public record DockerResult(int ExitCode, string Output, string Error);
