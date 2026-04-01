namespace ClawOS.Contracts.Skills;

public class ToolContext(string? arguments)
{
    public string? Arguments { get; init; } = arguments;

    /// <summary>
    /// The user ID of the person executing this tool.
    /// Used for workspace isolation and per-user resource access.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Whether the executing user has SuperAdmin privileges.
    /// SuperAdmin can access all user workspaces.
    /// </summary>
    public bool IsSuperAdmin { get; init; }
}