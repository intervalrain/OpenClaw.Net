using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using OpenClaw.Contracts.Skills;

namespace OpenClaw.Tools.Git.GitCommand;

public class GitSkill : AgentToolBase<GitSkillArgs>
{
    public override string Name => "git";
    public override string Description => """
        Local git operations. Use when: checking repository status, viewing commit history,
        creating/switching branches, staging/commiting changes, viewing diffs, or managing stashes.
        Requires git CLI installed.
        """;

    private const int maxLength = 50000;

    public override async Task<ToolResult> ExecuteAsync(GitSkillArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Command))
            return ToolResult.Failure("Command is required. Example: 'status', 'log --oneline -10'");

        var command = args.Command.Trim();

        // Remove 'git ' prefix if present
        if (command.StartsWith("git ", StringComparison.OrdinalIgnoreCase))
        {
            command = command[4..].Trim();
        }

        try
        {
            var result = await ExecuteGitCommandAsync(command, args.WorkingDirectory, ct);
            return result;
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Failure("Command timed out.");
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Git command failed: {ex.Message}");
        }
    }

    private static async Task<ToolResult> ExecuteGitCommandAsync(
        string command,
        string? workingDirectory,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = command,
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

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        
        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return ToolResult.Failure("Git command timed out after 60 seconds.");
        }

        var output = outputBuilder.ToString().Trim();
        var error = errorBuilder.ToString().Trim();

        if (process.ExitCode != 0)
        {
            var errorMessage = !string.IsNullOrEmpty(error) ? error : output;
            return ToolResult.Failure($"Exit code {process.ExitCode}: {errorMessage}");
        }

        if (output.Length > maxLength)
        {
            output = output[..maxLength] + $"\n... (truncated, total {output.Length} chars)";
        }

        return ToolResult.Success(string.IsNullOrEmpty(output) ? "(no output)" : output);
    }
}

public record GitSkillArgs(
    [property: Description("""
    Examples:
    - 'status' - Show working tree status
    - 'log --oneline -10' - Show last 10 commits
    - 'diff' - Show unstaged changes
    - 'branch -a' - List all branches
    - 'add .' - Stage all changes
    - 'commit -m "message"' - Commit staged changes 
    """)]
    string? Command,

    [property: Description("Working directory for the git command. Defaults to current directory.")]
    string? WorkingDirectory = null
);