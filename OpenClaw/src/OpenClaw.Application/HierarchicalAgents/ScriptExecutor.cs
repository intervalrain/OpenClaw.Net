using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Executes scripts (Python, Shell, Node.js) within workspace boundaries.
/// </summary>
public interface IScriptExecutor
{
    Task<ScriptResult> ExecuteAsync(string scriptPath, string? arguments = null,
        string? workingDirectory = null, TimeSpan? timeout = null, CancellationToken ct = default);
}

public record ScriptResult(bool IsSuccess, string Output, string? Error, int ExitCode);

public class ScriptExecutor(ILogger<ScriptExecutor> logger) : IScriptExecutor
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(300);

    public async Task<ScriptResult> ExecuteAsync(
        string scriptPath, string? arguments = null,
        string? workingDirectory = null, TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        // Validate script path exists
        if (!File.Exists(scriptPath))
            return new ScriptResult(false, "", $"Script not found: {scriptPath}", -1);

        // Validate the script is within the workspace base path
        var basePath = GetWorkspaceBasePath();
        var fullScriptPath = Path.GetFullPath(scriptPath);
        if (!fullScriptPath.StartsWith(Path.GetFullPath(basePath), StringComparison.OrdinalIgnoreCase))
            return new ScriptResult(false, "", "Security violation: script must be within workspace boundaries.", -1);

        // Reject path traversal attempts
        if (scriptPath.Contains(".."))
            return new ScriptResult(false, "", "Security violation: path traversal is not allowed.", -1);

        // Determine interpreter from extension
        var extension = Path.GetExtension(scriptPath).ToLowerInvariant();
        var (interpreter, interpreterArgs) = extension switch
        {
            ".py" => ("python3", scriptPath),
            ".sh" => ("bash", scriptPath),
            ".js" => ("node", scriptPath),
            _ => (null as string, null as string)
        };

        if (interpreter is null)
            return new ScriptResult(false, "", $"Unsupported script type: {extension}. Supported: .py, .sh, .js", -1);

        // Clamp timeout
        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (effectiveTimeout > MaxTimeout)
            effectiveTimeout = MaxTimeout;

        var workDir = workingDirectory ?? Path.GetDirectoryName(scriptPath) ?? ".";

        var fullArgs = string.IsNullOrWhiteSpace(arguments)
            ? interpreterArgs
            : $"{interpreterArgs} {arguments}";

        logger.LogInformation("Executing script: {Interpreter} {Args} in {WorkDir} (timeout: {Timeout}s)",
            interpreter, fullArgs, workDir, effectiveTimeout.TotalSeconds);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = interpreter,
                Arguments = fullArgs!,
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Clean environment: do not propagate host environment variables
            psi.Environment.Clear();
            // Only set minimal required env vars
            psi.Environment["PATH"] = "/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin";
            psi.Environment["HOME"] = workDir;
            psi.Environment["LANG"] = "en_US.UTF-8";

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Read output and error concurrently to avoid deadlocks
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(effectiveTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout (not user cancellation)
                process.Kill(entireProcessTree: true);
                var partialOut = await stdoutTask;
                return new ScriptResult(false, partialOut, "Script execution timed out.", -1);
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var success = process.ExitCode == 0;

            logger.LogInformation("Script completed with exit code {ExitCode}", process.ExitCode);

            return new ScriptResult(success, stdout, string.IsNullOrEmpty(stderr) ? null : stderr, process.ExitCode);
        }
        catch (OperationCanceledException)
        {
            return new ScriptResult(false, "", "Script execution was cancelled.", -1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute script: {ScriptPath}", scriptPath);
            return new ScriptResult(false, "", $"Failed to execute script: {ex.Message}", -1);
        }
    }

    private static string GetWorkspaceBasePath()
    {
        return Environment.GetEnvironmentVariable("OPENCLAW_WORKSPACE_PATH")
               ?? Path.Combine(AppContext.BaseDirectory, "workspace");
    }
}
