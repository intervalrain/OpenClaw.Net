namespace OpenClaw.Contracts.Skills;

public static class ToolPermissionChecker
{
    /// <summary>
    /// Checks if the user has sufficient permission to use the tool.
    /// </summary>
    public static bool HasPermission(IAgentTool tool, ToolContext context)
    {
        return tool.PermissionLevel switch
        {
            ToolPermissionLevel.Public => true,
            ToolPermissionLevel.WorkspaceAdmin => context.IsWorkspaceAdmin || context.IsSuperAdmin,
            ToolPermissionLevel.SuperAdmin => context.IsSuperAdmin,
            _ => false
        };
    }

    public static string GetDenialMessage(IAgentTool tool) =>
        $"Permission denied: '{tool.Name}' requires {tool.PermissionLevel} access.";
}
