using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.Domain.Updates.Entities;
using OpenClaw.Domain.Updates.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Updates;

/// <summary>
/// Background service that periodically checks GitHub for new releases of OpenClaw.NET
/// and records them for super admin review.
/// </summary>
public class GitHubReleaseChecker(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubReleaseChecker> logger) : BackgroundService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/intervalrain/openclaw.net/releases/latest";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 2 minutes after startup
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForNewReleaseAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check for GitHub release updates");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckForNewReleaseAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "OpenClaw.NET-UpdateChecker");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

        var response = await client.GetAsync(GitHubApiUrl, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("GitHub API returned {StatusCode} when checking for releases", response.StatusCode);
            return;
        }

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(ct);
        if (release is null || string.IsNullOrEmpty(release.TagName))
            return;

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISystemUpdateRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // Check if we already know about this release
        var existing = await repository.GetByTagNameAsync(release.TagName, ct);
        if (existing is not null)
            return;

        var update = SystemUpdate.Create(
            release.TagName,
            release.Name ?? release.TagName,
            release.Body,
            release.HtmlUrl,
            release.PublishedAt ?? DateTime.UtcNow);

        await repository.AddAsync(update, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation("New release detected: {TagName} - {ReleaseName}", release.TagName, release.Name);
    }

    private record GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = null!;

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("published_at")]
        public DateTime? PublishedAt { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }
    }
}
