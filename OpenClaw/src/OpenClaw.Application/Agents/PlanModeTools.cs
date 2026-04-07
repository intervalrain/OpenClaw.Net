using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Agents;

/// <summary>
/// Enters plan mode — restricts the agent to read-only tools for exploration.
/// The agent should explore the codebase, design a plan, then call exit_plan_mode.
///
/// Ref: Claude Code EnterPlanModeTool — transitions session to read-only exploration.
/// </summary>
public class EnterPlanModeTool : IAgentTool
{
    public string Name => "enter_plan_mode";
    public string Description =>
        "Enter planning mode. In this mode, you can only use read-only tools (read_file, " +
        "list_directory, search, git operations) to explore the codebase and design a plan. " +
        "Call exit_plan_mode with your plan when ready to execute.";

    public object? Parameters => new ToolParameters
    {
        Properties = new Dictionary<string, ToolProperty>
        {
            ["reason"] = new() { Type = "string", Description = "Why you're entering plan mode" }
        }
    };

    /// <summary>
    /// Reference to the pipeline's plan mode context. Set by the pipeline before execution.
    /// </summary>
    public PlanModeContext? Context { get; set; }

    public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
    {
        if (Context is null)
            return Task.FromResult(ToolResult.Failure("Plan mode context not available"));

        if (Context.State == PlanModeState.Planning)
            return Task.FromResult(ToolResult.Failure("Already in plan mode"));

        var args = JsonSerializer.Deserialize<EnterPlanArgs>(context.Arguments ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Context.State = PlanModeState.Planning;

        return Task.FromResult(ToolResult.Success(
            $"Entered plan mode. You can now only use read-only tools to explore.\n" +
            $"Available tools: {string.Join(", ", PlanModeContext.ReadOnlyTools)}\n" +
            $"Call exit_plan_mode with your plan when ready."));
    }

    private record EnterPlanArgs
    {
        [JsonPropertyName("reason")] public string? Reason { get; init; }
    }
}

/// <summary>
/// Exits plan mode — submits the plan and restores full tool access.
///
/// Ref: Claude Code ExitPlanModeV2Tool — approves plan, re-enables write tools.
/// </summary>
public class ExitPlanModeTool : IAgentTool
{
    public string Name => "exit_plan_mode";
    public string Description =>
        "Exit planning mode and submit your implementation plan. " +
        "After calling this, all tools become available for execution.";

    public object? Parameters => new ToolParameters
    {
        Properties = new Dictionary<string, ToolProperty>
        {
            ["plan"] = new() { Type = "string", Description = "The implementation plan to execute" }
        },
        Required = ["plan"]
    };

    public PlanModeContext? Context { get; set; }

    public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
    {
        if (Context is null)
            return Task.FromResult(ToolResult.Failure("Plan mode context not available"));

        if (Context.State != PlanModeState.Planning)
            return Task.FromResult(ToolResult.Failure("Not currently in plan mode"));

        var args = JsonSerializer.Deserialize<ExitPlanArgs>(context.Arguments ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrWhiteSpace(args?.Plan))
            return Task.FromResult(ToolResult.Failure("A plan is required to exit plan mode"));

        Context.Plan = args.Plan;
        Context.State = PlanModeState.Approved;

        return Task.FromResult(ToolResult.Success(
            "Plan approved. All tools are now available. Proceeding with execution."));
    }

    private record ExitPlanArgs
    {
        [JsonPropertyName("plan")] public string? Plan { get; init; }
    }
}
