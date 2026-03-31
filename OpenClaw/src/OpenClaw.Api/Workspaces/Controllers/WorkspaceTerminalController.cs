using System.Diagnostics;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Domain.Users.Repositories;
using OpenClaw.Tools.FileSystem;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

namespace OpenClaw.Api.Workspaces.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class WorkspaceTerminalController(
    ICurrentUserProvider currentUserProvider,
    IUserRepository userRepository,
    IConfigStore configStore) : ApiController
{
    private const long DefaultQuotaMb = 100;

    /// <summary>
    /// Commands that may write to disk and need quota enforcement.
    /// </summary>
    private static readonly HashSet<string> WritingCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "git", "cp", "mv", "touch", "mkdir", "npm", "dotnet", "python", "python3", "node"
    };
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "ls", "dir", "pwd", "echo", "cat", "head", "tail",
        "grep", "find", "wc", "date", "whoami", "hostname",
        "mkdir", "touch", "cp", "mv", "rm",
        "git", "dotnet", "npm", "node", "python", "python3"
    };

    private static readonly HashSet<string> BlockedPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "sudo", "su ", "rm -rf", "rm -fr", "mkfs", "dd ",
        ":(){", "fork", "> /dev/", "chmod 777", "chmod -R",
        "curl | sh", "curl | bash", "wget | sh", "wget | bash",
        "eval ", "exec ",
        ".env", "credentials", "secrets", "id_rsa", "private_key"
    };

    /// <summary>
    /// Execute a command within the user's workspace directory.
    /// </summary>
    [HttpPost("exec")]
    public async Task<IActionResult> Execute([FromBody] TerminalExecRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
            return BadRequest("Command is required.");

        var user = currentUserProvider.GetCurrentUser();
        var isSuperAdmin = user.Roles.Contains(Role.SuperAdmin);

        // Resolve working directory
        var cwd = string.IsNullOrWhiteSpace(request.Cwd)
            ? PathSecurity.GetUserWorkspacePath(user.Id)
            : PathSecurity.ResolveUserPath(request.Cwd.TrimStart('/'), user.Id, isSuperAdmin);

        var pathError = PathSecurity.ValidatePath(cwd, user.Id, isSuperAdmin);
        if (pathError is not null) return BadRequest(pathError);

        if (!Directory.Exists(cwd))
            return BadRequest("Working directory does not exist.");

        // Normalize and validate command
        var normalized = System.Text.RegularExpressions.Regex.Replace(
            request.Command.Trim(), @"\s+", " ");

        var blocked = BlockedPatterns.FirstOrDefault(p =>
            normalized.Contains(p, StringComparison.OrdinalIgnoreCase));
        if (blocked is not null)
            return BadRequest($"Command contains blocked pattern: '{blocked}'");

        var commandName = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (!AllowedCommands.Contains(commandName))
            return BadRequest($"Command '{commandName}' is not allowed. Allowed: {string.Join(", ", AllowedCommands.Order())}");

        // Quota check before writing commands
        var isWriteCommand = WritingCommands.Contains(commandName);
        if (isWriteCommand && !isSuperAdmin)
        {
            var quotaError = await CheckQuotaAsync(user.Id, ct);
            if (quotaError is not null)
                return Ok(new { Output = quotaError, ExitCode = -1 });
        }

        try
        {
            var result = await RunCommandAsync(request.Command, cwd, ct);

            // Post-execution quota check for write commands
            if (isWriteCommand && !isSuperAdmin)
            {
                var postQuotaError = await CheckQuotaAsync(user.Id, ct);
                if (postQuotaError is not null)
                    result = (result.output + $"\n\nWARNING: {postQuotaError}", result.exitCode);
            }

            return Ok(new { Output = result.output, ExitCode = result.exitCode });
        }
        catch (OperationCanceledException)
        {
            return Ok(new { Output = "Command timed out.", ExitCode = -1 });
        }
        catch (Exception ex)
        {
            return Ok(new { Output = $"Error: {ex.Message}", ExitCode = -1 });
        }
    }

    private static async Task<(string output, int exitCode)> RunCommandAsync(
        string command, string workingDirectory, CancellationToken ct)
    {
        var isWindows = OperatingSystem.IsWindows();
        var shell = isWindows ? "cmd.exe" : "/bin/bash";
        var shellArgs = isWindows ? $"/c {command}" : $"-c \"{command.Replace("\"", "\\\"")}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = shellArgs,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
        var error = await process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);

        var result = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";

        // Truncate
        if (result.Length > 5000)
            result = result[..5000] + $"\n... (truncated, total {result.Length} chars)";

        return (result, process.ExitCode);
    }

    private async Task<string?> CheckQuotaAsync(Guid userId, CancellationToken ct)
    {
        var workspacePath = PathSecurity.GetUserWorkspacePath(userId);
        var currentUsage = GetDirectorySize(workspacePath);
        var quotaMb = await GetQuotaMbAsync(userId, ct);
        var quotaBytes = quotaMb * 1024 * 1024;

        if (currentUsage > quotaBytes)
        {
            var usedMb = Math.Round((double)currentUsage / (1024 * 1024), 1);
            return $"Workspace quota exceeded ({usedMb} MB / {quotaMb} MB). Delete files to free space.";
        }

        return null;
    }

    private async Task<long> GetQuotaMbAsync(Guid userId, CancellationToken ct)
    {
        var dbUser = await userRepository.GetByIdAsync(userId, ct);
        if (dbUser?.WorkspaceQuotaMb is not null)
            return dbUser.WorkspaceQuotaMb.Value;

        var configValue = configStore.Get("WORKSPACE_QUOTA_MB");
        if (long.TryParse(configValue, out var configQuota))
            return configQuota;

        return DefaultQuotaMb;
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return new DirectoryInfo(path)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}

public record TerminalExecRequest(string Command, string? Cwd = null);
