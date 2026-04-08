using System.Text.Json;
using System.Text.RegularExpressions;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.CronJobs.Repositories;

namespace OpenClaw.Application.Skills;

/// <summary>
/// Resolves #instanceName references in text to pre-configured tool instances.
/// Shared by AgentPipeline (chat) and CronJobExecutor.
/// </summary>
public interface IToolInstanceResolver
{
    /// <summary>
    /// Scans content for #instanceName patterns, resolves tool instances,
    /// and returns the extra tools + pre-filled args.
    /// </summary>
    Task<ToolInstanceResolution> ResolveAsync(
        string content, Guid? userId, CancellationToken ct = default);
}

public record ToolInstanceResolution(
    List<ToolDefinition> ExtraToolDefs,
    Dictionary<string, IAgentTool> ExtraToolMap,
    Dictionary<string, string> InstanceArgs);

public partial class ToolInstanceResolver(
    IToolInstanceRepository toolInstanceRepo,
    IToolRegistry toolRegistry) : IToolInstanceResolver
{
    [GeneratedRegex(@"#([\w-]+)")]
    private static partial Regex InstanceRefRegex();

    public async Task<ToolInstanceResolution> ResolveAsync(
        string content, Guid? userId, CancellationToken ct = default)
    {
        var toolDefs = new List<ToolDefinition>();
        var toolMap = new Dictionary<string, IAgentTool>(StringComparer.OrdinalIgnoreCase);
        var instanceArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(content))
            return new(toolDefs, toolMap, instanceArgs);

        var matches = InstanceRefRegex().Matches(content);
        foreach (Match match in matches)
        {
            var instanceName = match.Groups[1].Value;

            // Try as tool instance (user-configured)
            if (userId.HasValue)
            {
                var instance = await toolInstanceRepo.GetByNameAsync(instanceName, userId.Value);
                if (instance is not null)
                {
                    var tool = toolRegistry.GetSkill(instance.ToolName);
                    if (tool is not null && !toolMap.ContainsKey(tool.Name))
                    {
                        toolDefs.Add(new ToolDefinition(tool.Name, tool.Description, tool.Parameters));
                        toolMap[tool.Name] = tool;
                        if (!string.IsNullOrEmpty(instance.ArgsJson))
                            instanceArgs[tool.Name] = instance.ArgsJson;
                    }
                    continue;
                }
            }

            // Fallback: try as direct tool name
            var directTool = toolRegistry.GetSkill(instanceName);
            if (directTool is not null && !toolMap.ContainsKey(directTool.Name))
            {
                toolDefs.Add(new ToolDefinition(directTool.Name, directTool.Description, directTool.Parameters));
                toolMap[directTool.Name] = directTool;
            }
        }

        return new(toolDefs, toolMap, instanceArgs);
    }

    /// <summary>
    /// Merges pre-filled tool instance args with LLM-provided args.
    /// Tool instance args (user-configured) take precedence over LLM args.
    /// </summary>
    public static string MergeToolArgs(Dictionary<string, string> instanceArgs, string toolName, string? llmArgs)
    {
        if (!instanceArgs.TryGetValue(toolName, out var prefilledJson))
            return llmArgs ?? "{}";

        try
        {
            var llmParsed = !string.IsNullOrEmpty(llmArgs)
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(llmArgs) ?? []
                : new Dictionary<string, JsonElement>();
            var prefilled = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(prefilledJson) ?? [];

            foreach (var (key, value) in prefilled)
                llmParsed[key] = value;

            return JsonSerializer.Serialize(llmParsed);
        }
        catch
        {
            return prefilledJson;
        }
    }
}
