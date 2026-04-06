namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Plan mode state machine for two-phase agent execution.
/// Ref: Claude Code EnterPlanModeTool / ExitPlanModeV2Tool —
/// read-only exploration phase, then approved execution phase.
/// </summary>
public enum PlanModeState
{
    /// <summary>Normal execution mode — all tools available.</summary>
    Normal,

    /// <summary>Plan mode — only read-only tools available for exploration.</summary>
    Planning,

    /// <summary>Plan approved — transitioning back to execution with full tools.</summary>
    Approved
}

/// <summary>
/// Tracks plan mode state within a pipeline execution.
/// Stored in AgentContext.Items["PlanMode"].
/// </summary>
public class PlanModeContext
{
    public PlanModeState State { get; set; } = PlanModeState.Normal;
    public string? Plan { get; set; }

    /// <summary>
    /// Tool names that are allowed during planning phase (read-only tools).
    /// </summary>
    public static readonly HashSet<string> ReadOnlyTools =
    [
        "read_file", "list_directory", "search_files", "grep_search",
        "git_status", "git_log", "git_diff",
        "web_search", "http_request",
        "tool_search",
        "enter_plan_mode", "exit_plan_mode"
    ];

    /// <summary>
    /// Checks if a tool is allowed in the current mode.
    /// </summary>
    public bool IsToolAllowed(string toolName) => State switch
    {
        PlanModeState.Planning => ReadOnlyTools.Contains(toolName),
        _ => true
    };
}
