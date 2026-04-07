namespace OpenClaw.Contracts.Skills;

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    object? Parameters { get; }

    /// <summary>
    /// Minimum permission level required to use this tool.
    /// Default: Public (all authenticated users).
    /// </summary>
    ToolPermissionLevel PermissionLevel => ToolPermissionLevel.Public;

    Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default);
}