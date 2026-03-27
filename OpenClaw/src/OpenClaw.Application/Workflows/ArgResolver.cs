using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Domain.Chat.Enums;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Workflows;

/// <summary>
/// Resolves skill arguments with LLM-assisted filling:
/// 1. Resolve explicitly set args (FilledValue, ConfigKey, InputMapping, UserPreference)
/// 2. For remaining unresolved args, use LLM to fill them based on upstream outputs + skill schema
/// </summary>
public class ArgResolver(
    IConfigStore configStore,
    IUserPreferenceRepository userPreferenceRepository,
    ILlmProviderFactory llmProviderFactory,
    ILogger<ArgResolver> logger)
{
    public async Task<string> ResolveArgsJsonAsync(
        SkillNode node,
        IAgentTool skill,
        Dictionary<string, string> nodeOutputs,
        Dictionary<string, string> nodeInputs,
        Guid? userId,
        CancellationToken ct)
    {
        // Phase 1: Resolve explicitly set args from node.Args
        var resolvedArgs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (node.Args is not null)
        {
            foreach (var (paramName, argSource) in node.Args)
            {
                var value = await ResolveArgValueAsync(paramName, argSource, nodeOutputs, userId, ct);
                if (value is not null)
                {
                    resolvedArgs[paramName] = value;
                }
            }
        }

        // Phase 2: Check all skill params — any param not yet resolved is a candidate for LLM
        var skillParams = (skill.Parameters as ToolParameters)?.Properties?.Keys
            ?? (IEnumerable<string>)Array.Empty<string>();
        var unresolvedParams = skillParams.Where(p => !resolvedArgs.ContainsKey(p)).ToList();

        if (unresolvedParams.Count > 0 && nodeOutputs.Count > 0)
        {
            var llmFilled = await LlmResolveArgsAsync(skill, resolvedArgs, nodeOutputs, nodeInputs, ct);
            if (llmFilled is not null)
            {
                foreach (var (key, value) in llmFilled)
                {
                    // User-set values always win — only fill truly unresolved args
                    if (!resolvedArgs.ContainsKey(key) && value is not null)
                    {
                        resolvedArgs[key] = value;
                    }
                }
            }
        }

        return JsonSerializer.Serialize(resolvedArgs, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    /// <summary>
    /// Calls LLM with upstream outputs + skill tool definition.
    /// LLM responds with a tool_use containing the filled args.
    /// </summary>
    private async Task<Dictionary<string, object?>?> LlmResolveArgsAsync(
        IAgentTool skill,
        Dictionary<string, object?> alreadyResolved,
        Dictionary<string, string> nodeOutputs,
        Dictionary<string, string> nodeInputs,
        CancellationToken ct)
    {
        try
        {
            var llmProvider = await llmProviderFactory.GetProviderAsync(ct);

            // Build upstream context (inputs + outputs)
            var upstreamParts = new List<string>();
            foreach (var (nodeId, output) in nodeOutputs)
            {
                var part = $"=== Node '{nodeId}' ===\n";
                if (nodeInputs.TryGetValue(nodeId, out var input))
                {
                    part += $"Input args: {input}\n";
                }
                part += $"Output: {output}";
                upstreamParts.Add(part);
            }
            var upstreamContext = string.Join("\n\n", upstreamParts);

            // Build already-resolved info
            var resolvedInfo = alreadyResolved.Count > 0
                ? $"\n\nAlready resolved arguments (DO NOT override these):\n{JsonSerializer.Serialize(alreadyResolved, new JsonSerializerOptions { WriteIndented = true })}"
                : "";

            var systemPrompt = $"""
                You are a workflow orchestrator. Your job is to call the tool "{skill.Name}" with the correct arguments based on the upstream node outputs provided below.

                Rules:
                - Analyze the upstream outputs and determine the best arguments for the tool call.
                - You MUST call the tool. Do not respond with text.
                - You MUST include ALL of the following pre-set arguments exactly as shown:{resolvedInfo}
                - Fill in any remaining arguments based on the upstream outputs.
                """;

            var userMessage = $"Based on the following upstream results, call the '{skill.Name}' tool:\n\n{upstreamContext}";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userMessage)
            };

            var tools = new List<ToolDefinition>
            {
                new(skill.Name, skill.Description, skill.Parameters)
            };

            logger.LogInformation("LLM arg resolve for skill {Skill} with {OutputCount} upstream outputs",
                skill.Name, nodeOutputs.Count);

            var response = await llmProvider.ChatAsync(messages, tools, ct);

            if (response.HasToolCalls)
            {
                var toolCall = response.ToolCalls!.First(tc => tc.Name == skill.Name);
                var argsJson = toolCall.Arguments;

                logger.LogInformation("LLM resolved args for {Skill}: {Args}", skill.Name, argsJson);

                return JsonSerializer.Deserialize<Dictionary<string, object?>>(argsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            logger.LogWarning("LLM did not return tool call for {Skill}, response: {Content}",
                skill.Name, response.Content);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LLM arg resolution failed for skill {Skill}", skill.Name);
            return null;
        }
    }

    private async Task<object?> ResolveArgValueAsync(
        string paramName,
        ArgSource source,
        Dictionary<string, string> nodeOutputs,
        Guid? userId,
        CancellationToken ct)
    {
        // Priority 1: FilledValue
        if (!string.IsNullOrEmpty(source.FilledValue))
        {
            logger.LogDebug("Resolved {Param} from FilledValue: {Value}", paramName, source.FilledValue);
            return ParseJsonValue(source.FilledValue);
        }

        // Priority 2: ConfigKey (system configuration from IConfigStore)
        if (!string.IsNullOrEmpty(source.ConfigKey))
        {
            var configValue = configStore.Get(source.ConfigKey);
            if (configValue is not null)
            {
                logger.LogDebug("Resolved {Param} from ConfigStore {Key}: {Value}",
                    paramName, source.ConfigKey, configValue);
                return ParseJsonValue(configValue);
            }
        }

        // Priority 3: InputMapping (upstream node output)
        if (!string.IsNullOrEmpty(source.InputMapping))
        {
            var value = ResolveInputMapping(source.InputMapping, nodeOutputs);
            if (value is not null)
            {
                logger.LogDebug("Resolved {Param} from InputMapping {Mapping}: {Value}",
                    paramName, source.InputMapping, value);
                return value;
            }
        }

        // Priority 4: UserPreferenceKey
        if (!string.IsNullOrEmpty(source.UserPreferenceKey) && userId.HasValue)
        {
            var preference = await userPreferenceRepository.GetByKeyAsync(
                userId.Value,
                source.UserPreferenceKey,
                ct);

            if (preference?.Value is not null)
            {
                logger.LogDebug("Resolved {Param} from UserPreference {Key}: {Value}",
                    paramName, source.UserPreferenceKey, preference.Value);
                return ParseJsonValue(preference.Value);
            }
        }

        logger.LogDebug("Could not resolve {Param}, will defer to LLM", paramName);
        return null;
    }

    private object? ResolveInputMapping(string mapping, Dictionary<string, string> nodeOutputs)
    {
        var dotIndex = mapping.IndexOf('.');
        string nodeId;
        string? jsonPath = null;

        if (dotIndex > 0)
        {
            nodeId = mapping[..dotIndex];
            jsonPath = mapping[(dotIndex + 1)..];
        }
        else
        {
            nodeId = mapping;
        }

        if (!nodeOutputs.TryGetValue(nodeId, out var outputJson) || string.IsNullOrEmpty(outputJson))
        {
            return null;
        }

        if (string.IsNullOrEmpty(jsonPath))
        {
            return ParseJsonValue(outputJson);
        }

        try
        {
            var node = JsonNode.Parse(outputJson);
            if (node is null) return null;

            var pathParts = jsonPath.Split('.');
            foreach (var part in pathParts)
            {
                if (part.Contains('[') && part.EndsWith(']'))
                {
                    var bracketIndex = part.IndexOf('[');
                    var fieldName = part[..bracketIndex];
                    var indexStr = part[(bracketIndex + 1)..^1];

                    if (!string.IsNullOrEmpty(fieldName))
                    {
                        node = node[fieldName];
                    }

                    if (node is JsonArray arr && int.TryParse(indexStr, out var index))
                    {
                        node = arr[index];
                    }
                }
                else
                {
                    node = node?[part];
                }

                if (node is null) return null;
            }

            return node.GetValue<object>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse JSON path {Path} from output", jsonPath);
            return null;
        }
    }

    private static object? ParseJsonValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        try
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                return JsonSerializer.Deserialize<object>(value);
            }

            if (int.TryParse(value, out var intVal)) return intVal;
            if (double.TryParse(value, out var doubleVal)) return doubleVal;
            if (bool.TryParse(value, out var boolVal)) return boolVal;

            return value;
        }
        catch
        {
            return value;
        }
    }
}
