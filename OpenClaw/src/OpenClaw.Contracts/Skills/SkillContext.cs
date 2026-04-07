namespace OpenClaw.Contracts.Skills;

public class ToolContext(string? arguments)
{
    public string? Arguments { get; init; } = arguments;

    /// <summary>
    /// The user ID of the person executing this tool.
    /// Used for workspace isolation and per-user resource access.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// The active workspace for this tool execution.
    /// File operations resolve paths relative to this workspace.
    /// </summary>
    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// The executing user's roles (from CurrentUser.Roles).
    /// Used by ToolPermissionChecker for RBAC decisions.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Whether the executing user has SuperAdmin privileges.
    /// Derived from Roles containing "SuperAdmin".
    /// </summary>
    public bool IsSuperAdmin => Roles.Any(r => r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether the executing user is an admin of the current workspace.
    /// Derived from Roles containing "Admin" or "SuperAdmin".
    /// </summary>
    public bool IsWorkspaceAdmin => IsSuperAdmin || Roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));
}