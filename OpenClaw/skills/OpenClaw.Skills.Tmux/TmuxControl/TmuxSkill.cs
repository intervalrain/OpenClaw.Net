using System.ComponentModel;
using System.Diagnostics;
using System.Text;

using OpenClaw.Contracts.Skills;

namespace OpenClaw.Skills.Tmux.TmuxControl;

public class TmuxSkill : AgentSkillBase<TmuxSkillArgs>
{
    public override string Name => "tmux";
    public override string Description => """
        Tmux session management. Use when: listing tmux sessions, creating/killing sessions,
        sending commands to sessions, capturing pane content, or managing windows/panes.
        Requires tmux installed.
        """;

    public override async Task<SkillResult> ExecuteAsync(TmuxSkillArgs args, CancellationToken ct)
    {
        var tmuxPath = await FindTmuxPathAsync(ct);
        if (tmuxPath == null)
            return SkillResult.Failure("tmux is not installed or not in PATH.");

        try
        {
            return args.Operation.ToLowerInvariant() switch
            {
                "list" or "ls" => await ListSessionsAsync(ct),
                "new" or "create" => await NewSessionAsync(args.SessionName, args.Command, ct),
                "kill" => await KillSessionAsync(args.SessionName, ct),
                "send" or "send_keys" => await SendKeysAsync(args.SessionName, args.Keys, args.Window, args.Pane, ct),
                "capture" => await CapturePaneAsync(args.SessionName, args.Window, args.Pane, args.Lines, ct),
                "list_windows" => await ListWindowsAsync(args.SessionName, ct),
                "has_session" => await HasSessionAsync(args.SessionName, ct),
                _ => SkillResult.Failure($"Unknown operation: {args.Operation}. Valid: list, new, kill, send, capture, list_windows, has_session")
            };
        }
        catch (Exception ex)
        {
            return SkillResult.Failure($"Tmux error: {ex.Message}");
        }
    }

    private static async Task<string?> FindTmuxPathAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = "tmux",
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

    private static async Task<SkillResult> RunTmuxCommandAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "tmux",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return SkillResult.Failure("Tmux command timed out after 30 seconds.");
        }

        var output = outputBuilder.ToString().Trim();
        var error = errorBuilder.ToString().Trim();

        if (process.ExitCode != 0)
        {
            var errorMessage = !string.IsNullOrEmpty(error) ? error : output;

            if (errorMessage.Contains("no server running"))
                return SkillResult.Success("No tmux sessions running.");

            return SkillResult.Failure($"Exit code {process.ExitCode}: {errorMessage}");
        }

        return SkillResult.Success(string.IsNullOrEmpty(output) ? "(no output)" : output);
    }

    private static Task<SkillResult> ListSessionsAsync(CancellationToken ct)
    {
        return RunTmuxCommandAsync("list-sessions -F '#{session_name}: #{session_windows} windows (created #{session_created_string})'", ct);
    }

    private static async Task<SkillResult> NewSessionAsync(string? sessionName, string? command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return SkillResult.Failure("sessionName is required for new operation.");

        var args = $"new-session -d -s {EscapeArg(sessionName)}";
        if (!string.IsNullOrWhiteSpace(command))
            args += $" {EscapeArg(command)}";

        var result = await RunTmuxCommandAsync(args, ct);
        if (result.IsSuccess)
            return SkillResult.Success($"Session '{sessionName}' created successfully.");

        return result;
    }

    private static async Task<SkillResult> KillSessionAsync(string? sessionName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return SkillResult.Failure("sessionName is required for kill operation.");

        var result = await RunTmuxCommandAsync($"kill-session -t {EscapeArg(sessionName)}", ct);
        if (result.IsSuccess)
            return SkillResult.Success($"Session '{sessionName}' killed successfully.");

        return result;
    }

    private static async Task<SkillResult> SendKeysAsync(string? sessionName, string? keys, string? window, string? pane, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return SkillResult.Failure("sessionName is required for send operation.");

        if (string.IsNullOrWhiteSpace(keys))
            return SkillResult.Failure("keys is required for send operation.");

        var target = sessionName;
        if (!string.IsNullOrWhiteSpace(window))
            target += $":{window}";
        if (!string.IsNullOrWhiteSpace(pane))
            target += $".{pane}";

        var result = await RunTmuxCommandAsync($"send-keys -t {EscapeArg(target)} {EscapeArg(keys)} Enter", ct);
        if (result.IsSuccess)
            return SkillResult.Success($"Keys sent to '{target}' successfully.");

        return result;
    }

    private static Task<SkillResult> CapturePaneAsync(string? sessionName, string? window, string? pane, int? lines, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return Task.FromResult(SkillResult.Failure("sessionName is required for capture operation."));

        var target = sessionName;
        if (!string.IsNullOrWhiteSpace(window))
            target += $":{window}";
        if (!string.IsNullOrWhiteSpace(pane))
            target += $".{pane}";

        var lineCount = lines ?? 100;
        return RunTmuxCommandAsync($"capture-pane -t {EscapeArg(target)} -p -S -{lineCount}", ct);
    }

    private static Task<SkillResult> ListWindowsAsync(string? sessionName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return Task.FromResult(SkillResult.Failure("sessionName is required for list_windows operation."));

        return RunTmuxCommandAsync($"list-windows -t {EscapeArg(sessionName)} -F '#{{window_index}}: #{{window_name}} (#{{window_panes}} panes)'", ct);
    }

    private static async Task<SkillResult> HasSessionAsync(string? sessionName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            return SkillResult.Failure("sessionName is required for has_session operation.");

        var result = await RunTmuxCommandAsync($"has-session -t {EscapeArg(sessionName)}", ct);

        if (result.IsSuccess)
            return SkillResult.Success($"Session '{sessionName}' exists.");

        if (result.Error?.Contains("can't find session") == true)
            return SkillResult.Success($"Session '{sessionName}' does not exist.");

        return result;
    }

    private static string EscapeArg(string arg)
    {
        if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('\''))
            return $"\"{arg.Replace("\"", "\\\"")}\"";
        return arg;
    }
}

public record TmuxSkillArgs(
    [property: Description("""
        The operation to perform:
        - list/ls: List all sessions
        - new/create: Create a new session (requires sessionName, optional command)
        - kill: Kill a session (requires sessionName)
        - send/send_keys: Send keys to a session (requires sessionName, keys)
        - capture: Capture pane content (requires sessionName, optional lines)
        - list_windows: List windows in a session (requires sessionName)
        - has_session: Check if a session exists (requires sessionName)
        """)]
    string Operation,

    [property: Description("Session name for operations that require it")]
    string? SessionName = null,

    [property: Description("Command to run when creating a new session")]
    string? Command = null,

    [property: Description("Keys to send (for send_keys operation)")]
    string? Keys = null,

    [property: Description("Window index or name (for send_keys/capture)")]
    string? Window = null,

    [property: Description("Pane index (for send_keys/capture)")]
    string? Pane = null,

    [property: Description("Number of lines to capture (default: 100)")]
    int? Lines = null
);
