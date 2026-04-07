using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Agents;

/// <summary>
/// Meta-tool that lets the LLM discover available tools by keyword search.
/// When tool count exceeds the deferred threshold, only core tools + this
/// tool_search are sent to the LLM. The LLM calls tool_search to find
/// and load additional tool schemas on demand.
///
/// Ref: Claude Code ToolSearchTool — deferred tool loading to save tokens.
/// </summary>
public class ToolSearchTool : IAgentTool
{
    private readonly IReadOnlyList<IAgentTool> _allTools;

    public ToolSearchTool(IEnumerable<IAgentTool> allTools)
    {
        _allTools = allTools.Where(t => t.Name != Name).ToList();
    }

    public string Name => "tool_search";

    public string Description =>
        "Search for available tools by keyword. Returns matching tool names and descriptions. " +
        "Use this when you need a tool that isn't in your current tool list. " +
        "After finding a tool, you can call it directly by name.";

    public object? Parameters => new ToolParameters
    {
        Properties = new Dictionary<string, ToolProperty>
        {
            ["query"] = new() { Type = "string", Description = "Keyword to search for (e.g., 'file', 'git', 'http', 'shell')" },
            ["max_results"] = new() { Type = "integer", Description = "Maximum number of results to return (default: 5)" }
        },
        Required = ["query"]
    };

    public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
    {
        var args = JsonSerializer.Deserialize<ToolSearchArgs>(context.Arguments ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var query = args?.Query?.Trim().ToLowerInvariant() ?? "";
        var maxResults = args?.MaxResults ?? 5;

        if (string.IsNullOrEmpty(query))
            return Task.FromResult(ToolResult.Failure("query parameter is required"));

        var matches = _allTools
            .Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            })
            .ToList();

        if (matches.Count == 0)
        {
            // Return all tool names as suggestions
            var allNames = string.Join(", ", _allTools.Select(t => t.Name));
            return Task.FromResult(ToolResult.Success(
                $"No tools matched '{query}'. Available tools: {allNames}"));
        }

        var json = JsonSerializer.Serialize(matches, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return Task.FromResult(ToolResult.Success(
            $"Found {matches.Count} tool(s) matching '{query}':\n\n{json}"));
    }

    private record ToolSearchArgs
    {
        [JsonPropertyName("query")]
        public string? Query { get; init; }

        [JsonPropertyName("max_results")]
        public int? MaxResults { get; init; }
    }
}
