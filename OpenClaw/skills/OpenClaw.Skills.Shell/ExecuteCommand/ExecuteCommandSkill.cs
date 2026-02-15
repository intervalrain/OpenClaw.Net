using System.ComponentModel;
using System.Diagnostics;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.Shell.ExecuteCommand;

public class ExecuteCommandSkill(
    HashSet<string>? allowedCommands = null,
    HashSet<string>? blockedPatterns = null,
    TimeSpan? timeout = null,
    int maxOutputLength = 1000) : AgentSkillBase<ExecuteCommandArgs>
{
    public static ExecuteCommandSkill Default => new(
        allowedCommands: DefaultAllowedCommands, 
        blockedPatterns: DefaultBlockedPatterns, 
        timeout: TimeSpan.FromSeconds(30), 
        maxOutputLength: 1000);
        
    private static readonly HashSet<string> DefaultAllowedCommands =
    [
        "ls", "dir", "pwd", "echo", "cat", "head", "tail",
        "grep", "find", "wc", "date", "whoami", "hostname",
        "dotnet", "git", "npm", "node", "python", "python3"
    ];

    private static readonly HashSet<string> DefaultBlockedPatterns =
    [
        "sudo", "su ", "rm -rf", "rm -fr", "mkfs", "dd ",
        ":(){", "fork", "> /dev/", "chmod 777", "chmod -R",
        "curl | sh", "curl | bash", "wget | sh", "wget | bash",
        "eval ", "exec ", "&&", "||", ";", "|", "`", "$(",
        "> ", ">> ", "< ", "<< "
    ];

    private readonly HashSet<string> _allowedCommands = allowedCommands ?? DefaultAllowedCommands;
    private readonly HashSet<string> _blockedPatterns = blockedPatterns ?? DefaultBlockedPatterns;
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(30);
    private readonly int _maxOutputLength = maxOutputLength;

    public override string Name => "execute_command";
    public override string Description => "Execute a shell command. Only allowed commands can be executed. Dangerous patterns are blocked";

    protected override async Task<SkillResult> ExecuteAsync(ExecuteCommandArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Command))
        {
            return SkillResult.Failure("Command is required.");
        }

        var blockedPattern = _blockedPatterns.FirstOrDefault(p =>
            args.Command.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (blockedPattern is not null)
        {
            return SkillResult.Failure($"Command contains blocked pattern: '{blockedPattern}'");
        }

        // Check allowed commands
        var commandName = args.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

        if (!_allowedCommands.Contains(commandName))
        {
            return SkillResult.Failure($"Command '{commandName}' is not allowed. Allowed: {string.Join(", ", _allowedCommands)}");
        }

        try
        {
            var result = await RunCommandAsync(args.Command, args.WorkingDirectory, ct);
            return SkillResult.Success(result);
        }
        catch (OperationCanceledException)
        {
            return SkillResult.Failure("Command execution timed out.");
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Command failed: {ex.Message}");
        }
    }

    private async Task<string> RunCommandAsync(string command, string? workingDirectory, CancellationToken ct)
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
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var errorTask = process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        var output = await outputTask;
        var error = await errorTask;

        var result = string.IsNullOrEmpty(error)
            ? output
            : $"{output}\n[stderr]\n{error}";

        if (result.Length > _maxOutputLength)
        {
            result = result[.._maxOutputLength] + $"\n... (truncated, total {result.Length} chars)";
        }

        return result;
    }
}
public record ExecuteCommandArgs(
    [property: Description("The shell command to execute")]
    string? Command,

    [property: Description("Working directory for the command. Defaults to current directory")]
    string? WorkingDirectory
);