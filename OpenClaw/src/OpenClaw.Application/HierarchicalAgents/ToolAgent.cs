using System.Text.Json;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Adapter that wraps an IAgentTool into an IAgent for backward compatibility.
/// Bridges the tool layer (LLM-callable functions) into the agent layer (composable units).
/// </summary>
public class ToolAgent : DeterministicAgent
{
    private readonly IAgentTool _tool;

    public ToolAgent(IAgentTool tool)
    {
        _tool = tool;
    }

    public override string Name => $"tool:{_tool.Name}";
    public override string Description => _tool.Description;

    public override JsonDocument? InputSchema =>
        _tool.Parameters is not null
            ? JsonDocument.Parse(JsonSerializer.Serialize(_tool.Parameters))
            : null;

    protected override async Task<AgentResult> ExecuteCoreAsync(
        AgentExecutionContext context, CancellationToken ct)
    {
        var arguments = context.Input.RootElement.ToString();
        var toolContext = new ToolContext(arguments);
        var result = await _tool.ExecuteAsync(toolContext, ct);

        if (result.IsSuccess)
        {
            var output = JsonDocument.Parse(
                JsonSerializer.Serialize(new { output = result.Output }));
            return AgentResult.Success(output);
        }

        return AgentResult.Failed(result.Error ?? "Tool execution failed.");
    }
}
