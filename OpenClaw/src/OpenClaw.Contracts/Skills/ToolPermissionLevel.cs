namespace OpenClaw.Contracts.Skills;

/// <summary>
/// Permission level required to use a tool.
/// Ref: Claude Code permissions.ts — multi-layer permission model.
/// </summary>
public enum ToolPermissionLevel
{
    /// <summary>Available to all authenticated users. Default.</summary>
    Public = 0,

    /// <summary>Requires workspace admin role.</summary>
    WorkspaceAdmin = 1,

    /// <summary>Requires super admin role.</summary>
    SuperAdmin = 2
}
