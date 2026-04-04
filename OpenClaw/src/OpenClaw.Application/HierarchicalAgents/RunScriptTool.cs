using System.ComponentModel;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

public record RunScriptArgs(
    [property: Description("Path to the script file relative to workspace root (e.g. agents/my-agent/scripts/fetch.py)")]
    string Path,
    [property: Description("Command line arguments to pass to the script")]
    string? Arguments
);

/// <summary>
/// Agent tool that executes scripts (Python/Shell/Node.js) from the workspace.
/// </summary>
public class RunScriptTool(IScriptExecutor scriptExecutor) : AgentToolBase<RunScriptArgs>
{
    public override string Name => "run_script";
    public override string Description =>
        "Executes a script (Python/Shell/Node.js) from the workspace. Scripts must be within the workspace boundary.";

    public override async Task<ToolResult> ExecuteAsync(RunScriptArgs args, ToolContext context, CancellationToken ct)
    {
        if (context.WorkspaceId is null || context.WorkspaceId == Guid.Empty)
            return ToolResult.Failure("Workspace context is required to run scripts.");

        if (string.IsNullOrWhiteSpace(args.Path))
            return ToolResult.Failure("Script path is required.");

        // Resolve the path within the workspace
        var workspacePath = ResolveWorkspacePath(context.WorkspaceId.Value);
        var absolutePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(workspacePath, args.Path));

        // Validate the resolved path is within workspace boundaries
        if (!absolutePath.StartsWith(System.IO.Path.GetFullPath(workspacePath), StringComparison.OrdinalIgnoreCase))
            return ToolResult.Failure("Access denied: script path must be within the workspace.");

        var result = await scriptExecutor.ExecuteAsync(absolutePath, args.Arguments, ct: ct);

        if (result.IsSuccess)
        {
            var output = result.Output;
            if (!string.IsNullOrEmpty(result.Error))
                output += $"\n[stderr]: {result.Error}";
            return ToolResult.Success(string.IsNullOrEmpty(output) ? "(no output)" : output);
        }

        var errorMsg = $"Script failed (exit code {result.ExitCode})";
        if (!string.IsNullOrEmpty(result.Error))
            errorMsg += $": {result.Error}";
        if (!string.IsNullOrEmpty(result.Output))
            errorMsg += $"\n[stdout]: {result.Output}";

        return ToolResult.Failure(errorMsg);
    }

    private static string ResolveWorkspacePath(Guid workspaceId)
    {
        var basePath = Environment.GetEnvironmentVariable("OPENCLAW_WORKSPACE_PATH")
                       ?? System.IO.Path.Combine(AppContext.BaseDirectory, "workspace");
        var wsPath = System.IO.Path.Combine(basePath, workspaceId.ToString());

        if (!Directory.Exists(wsPath))
            Directory.CreateDirectory(wsPath);

        return wsPath;
    }
}
