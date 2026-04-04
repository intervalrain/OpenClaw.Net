using System.ComponentModel;
using System.Text.Json;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

public record PioneerCreateArgs(
    [property: Description("Name of the agent to create (lowercase-kebab-case)")]
    string Name,
    [property: Description("The full AGENT.md content including frontmatter")]
    string AgentMd,
    [property: Description("JSON object of script files: { \"script_name.py\": \"script content\", ... }")]
    string? Scripts,
    [property: Description("Optional workflow.yaml DAG definition for multi-step agents")]
    string? WorkflowYaml
);

/// <summary>
/// Agent tool that allows the chat LLM to create new agents in a workspace.
/// </summary>
public class PioneerCreateTool(IPioneerCreateService createService) : AgentToolBase<PioneerCreateArgs>
{
    public override string Name => "create_agent";
    public override string Description =>
        "Creates a new agent in the workspace. Provide AGENT.md content and optional scripts (Python/Shell) and workflow DAG.";

    public override async Task<ToolResult> ExecuteAsync(PioneerCreateArgs args, ToolContext context, CancellationToken ct)
    {
        if (context.WorkspaceId is null || context.WorkspaceId == Guid.Empty)
            return ToolResult.Failure("Workspace context is required to create an agent.");

        // Parse scripts JSON into dictionary
        var scripts = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(args.Scripts))
        {
            try
            {
                scripts = JsonSerializer.Deserialize<Dictionary<string, string>>(args.Scripts,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (JsonException ex)
            {
                return ToolResult.Failure($"Invalid scripts JSON: {ex.Message}");
            }
        }

        var request = new CreateAgentRequest
        {
            WorkspaceId = context.WorkspaceId.Value,
            AgentName = args.Name,
            AgentMdContent = args.AgentMd,
            Scripts = scripts,
            WorkflowYaml = args.WorkflowYaml
        };

        var result = await createService.CreateAgentAsync(request, ct);

        if (result.IsSuccess)
            return ToolResult.Success($"Agent '{args.Name}' created at {result.AgentPath}");

        return ToolResult.Failure(result.ErrorMessage ?? "Unknown error creating agent.");
    }
}
