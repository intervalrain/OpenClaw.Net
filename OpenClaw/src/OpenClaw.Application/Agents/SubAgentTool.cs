using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Agents;

/// <summary>
/// Tool that spawns a sub-agent with isolated context to handle a delegated task.
/// The sub-agent runs a fresh AgentPipeline with its own conversation history,
/// optional system prompt, and optional tool subset.
///
/// Ref: Claude Code AgentTool — spawns sub-agents with color-coded output,
/// isolated context, and configurable tool restrictions.
/// </summary>
public class SubAgentTool : IAgentTool
{
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IReadOnlyList<IAgentTool> _allTools;
    private readonly int _currentDepth;
    private readonly int _maxDepth;

    public const int DefaultMaxDepth = 3;

    public SubAgentTool(
        ILlmProviderFactory llmProviderFactory,
        IEnumerable<IAgentTool> allTools,
        int currentDepth = 0,
        int maxDepth = DefaultMaxDepth)
    {
        _llmProviderFactory = llmProviderFactory;
        _allTools = allTools.Where(t => t.Name != Name).ToList();
        _currentDepth = currentDepth;
        _maxDepth = maxDepth;
    }

    public string Name => "spawn_agent";

    public string Description =>
        "Spawn a sub-agent to handle a delegated task independently. " +
        "The sub-agent has its own conversation context and returns a final result. " +
        "Use this for parallel research, verification, or tasks that benefit from isolation.";

    public object? Parameters => new ToolParameters
    {
        Properties = new Dictionary<string, ToolProperty>
        {
            ["task"] = new() { Type = "string", Description = "The task description for the sub-agent" },
            ["system_prompt"] = new() { Type = "string", Description = "Optional system prompt for the sub-agent" },
            ["tools"] = new()
            {
                Type = "array",
                Description = "Optional list of tool names the sub-agent can use. If empty, all tools are available.",
                Items = new ToolProperty { Type = "string" }
            }
        },
        Required = ["task"]
    };

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
    {
        if (_currentDepth >= _maxDepth)
            return ToolResult.Failure($"Maximum sub-agent depth ({_maxDepth}) reached. Cannot spawn deeper.");

        var args = JsonSerializer.Deserialize<SubAgentArgs>(context.Arguments ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrWhiteSpace(args?.Task))
            return ToolResult.Failure("task parameter is required");

        // Resolve tool subset
        var tools = ResolveTools(args.Tools);

        // Create a child spawn_agent with incremented depth
        var childSpawnAgent = new SubAgentTool(_llmProviderFactory, tools, _currentDepth + 1, _maxDepth);
        var toolsWithChild = tools.Concat([childSpawnAgent]).ToList();

        var pipelineOptions = new AgentPipelineOptions
        {
            SystemPrompt = args.SystemPrompt ?? "You are a helpful sub-agent. Complete the given task and return a concise result.",
            MaxIterations = 10
        };

        var pipeline = new AgentPipeline(
            _llmProviderFactory,
            toolsWithChild,
            pipelineOptions);

        try
        {
            var result = await pipeline.ExecuteAsync(
                args.Task,
                userId: context.UserId,
                workspaceId: context.WorkspaceId,
                userRoles: context.Roles,
                ct: ct);

            return ToolResult.Success(result);
        }
        catch (Exception ex)
        {
            return ToolResult.Failure($"Sub-agent failed: {ex.Message}");
        }
    }

    private List<IAgentTool> ResolveTools(List<string>? requestedTools)
    {
        if (requestedTools is null or { Count: 0 })
            return _allTools.ToList();

        var toolSet = new HashSet<string>(requestedTools, StringComparer.OrdinalIgnoreCase);
        return _allTools.Where(t => toolSet.Contains(t.Name)).ToList();
    }

    private record SubAgentArgs
    {
        [JsonPropertyName("task")] public string? Task { get; init; }
        [JsonPropertyName("system_prompt")] public string? SystemPrompt { get; init; }
        [JsonPropertyName("tools")] public List<string>? Tools { get; init; }
    }
}
