using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.GitHub;

/// <summary>
/// GitHub operations via gh CLI: issues, PRs, CI runs, code review, API queries.
/// Requires: gh CLI installed and authenticated (via GH_TOKEN env var or gh auth login)
/// </summary>
public class GitHubSkill : AgentSkillBase<GitHubSkillArgs>
{
    public override string Name => "github";
    public override string Description => """
        GitHub operations via gh CLI. Use when: checking PR/CI status, creating/listing issues or PRs,
        viewing workflow runs, or querying GitHub API. Requires gh CLI with GH_TOKEN or gh auth.
        """;

    public override async Task<SkillResult> ExecuteAsync(GitHubSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Command))
        {
            return SkillResult.Failure("Command is required. Example: 'pr list --repo owner/repo'");
        }

        // Check if gh is available
        var ghPath = await FindGhPathAsync(ct);
        if (ghPath == null)
        {
            return SkillResult.Failure("gh CLI is not installed. Please install it: https://cli.github.com/");
        }

        // Build the full command
        var ghCommand = args.Command.Trim();

        // If command doesn't start with common gh subcommands, treat it as raw gh command
        // Otherwise prepend 'gh' if needed
        if (!ghCommand.StartsWith("gh "))
        {
            ghCommand = $"gh {ghCommand}";
        }

        try
        {
            var result = await ExecuteGhCommandAsync(ghCommand, args.WorkingDirectory, ct);
            return result;
        }
        catch (OperationCanceledException)
        {
            return SkillResult.Failure("Command timed out.");
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Command failed: {ex.Message}");
        }
    }

    private static async Task<string?> FindGhPathAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = "gh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0 ? output.Trim().Split('\n')[0] : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<SkillResult> ExecuteGhCommandAsync(
        string command,
        string? workingDirectory,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            psi.WorkingDirectory = workingDirectory;
        }

        // GH_TOKEN will be automatically picked up from environment if set

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60)); // 60 second timeout

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return SkillResult.Failure("Command timed out after 60 seconds.");
        }

        var output = outputBuilder.ToString().Trim();
        var error = errorBuilder.ToString().Trim();

        if (process.ExitCode != 0)
        {
            var errorMessage = !string.IsNullOrEmpty(error) ? error : output;

            // Check for auth errors
            if (errorMessage.Contains("gh auth login") || errorMessage.Contains("not logged"))
            {
                return SkillResult.Failure(
                    "GitHub CLI is not authenticated. Set GH_TOKEN environment variable or run 'gh auth login'.");
            }

            return SkillResult.Failure($"Exit code {process.ExitCode}: {errorMessage}");
        }

        // Truncate if too long
        const int maxLength = 50000;
        if (output.Length > maxLength)
        {
            output = output[..maxLength] + $"\n... (truncated, total {output.Length} chars)";
        }

        return SkillResult.Success(string.IsNullOrEmpty(output) ? "(no output)" : output);
    }
}

public record GitHubSkillArgs(
    [property: Description("""
        The gh CLI command to execute (without 'gh' prefix).
        Examples:
        - 'pr list --repo owner/repo'
        - 'issue create --title "Bug" --body "Description"'
        - 'pr checks 55 --repo owner/repo'
        - 'run list --repo owner/repo --limit 5'
        - 'api repos/owner/repo/pulls/55 --jq ".title"'
        """)]
    string? Command,

    [property: Description("Optional working directory for the command (useful when --repo is not specified)")]
    string? WorkingDirectory
);
