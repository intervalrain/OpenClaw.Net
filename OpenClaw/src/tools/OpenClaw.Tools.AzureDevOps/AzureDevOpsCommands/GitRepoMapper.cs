using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;

using Microsoft.Extensions.Logging;

namespace OpenClaw.Tools.AzureDevOps.AzureDevOpsCommands;

public partial class GitRepoMapper(ILogger<GitRepoMapper> logger)
{
    // ADO URL patterns:
    // https://dev.azure.com/{org}/{project}/_git/{repo}
    // https://{org}@dev.azure.com/{org}/{project}/_git/{repo}
    // git@ssh.dev.azure.com:v3/{org}/{project}/{repo}
    // Note: project may be URL-encoded (e.g., IoT%20Platform)
    [GeneratedRegex(@"(?:https://)?(?:[\w-]+@)?(?:ssh\.)?dev\.azure\.com[:/](?:v3/)?(?<org>[\w-]+)/(?<project>[^/]+)/(?:_git/)?(?<repo>[\w-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AdoUrlPattern();

    public async Task<GitRepoInfo> GetRepoInfoAsync(string localPath, CancellationToken ct = default)
    {
        var resolvedPath = ResolvePathForContainer(localPath);
        var gitPath = Path.Combine(resolvedPath, ".git");

        logger.LogDebug("Resolving repo: original={OriginalPath}, resolved={ResolvedPath}, gitExists={GitExists}",
            localPath, resolvedPath, Directory.Exists(gitPath));

        if (!Directory.Exists(gitPath))
        {
            logger.LogDebug("No .git directory found at {Path}", gitPath);
            return new GitRepoInfo(localPath, null, null, null, null, false);
        }

        var remoteUrl = await GetGitRemoteUrlAsync(resolvedPath, ct);
        logger.LogDebug("Remote URL: {RemoteUrl}", remoteUrl ?? "null");

        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return new GitRepoInfo(localPath, null, null, null, null, false);
        }

        var match = AdoUrlPattern().Match(remoteUrl);
        logger.LogDebug("ADO URL regex match: {IsMatch}", match.Success);

        if (!match.Success)
        {
            return new GitRepoInfo(localPath, remoteUrl, null, null, null, false);
        }

        var org = match.Groups["org"].Value;
        var project = HttpUtility.UrlDecode(match.Groups["project"].Value);
        var repo = match.Groups["repo"].Value;

        logger.LogDebug("Parsed ADO repo - org={Org}, project={Project}, repo={Repo}", org, project, repo);

        return new GitRepoInfo(localPath, remoteUrl, org, project, repo, true);
    }

    public async Task<List<GitRepoInfo>> GetTrackedReposAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        var tasks = paths.Select(p => GetRepoInfoAsync(p, ct));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private static string ResolvePathForContainer(string hostPath)
    {
        var hostBasePath = Environment.GetEnvironmentVariable("TRACKED_PROJECTS_HOST_PATH");
        var containerBasePath = Environment.GetEnvironmentVariable("TRACKED_PROJECTS_CONTAINER_PATH");

        if (string.IsNullOrEmpty(hostBasePath) || string.IsNullOrEmpty(containerBasePath))
        {
            return hostPath;
        }

        if (hostPath.StartsWith(hostBasePath, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = hostPath[hostBasePath.Length..].TrimStart('/', '\\');
            return Path.Combine(containerBasePath, relativePath);
        }

        return hostPath;
    }

    private static async Task<string?> GetGitRemoteUrlAsync(string workingDirectory, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "remote get-url origin",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}
