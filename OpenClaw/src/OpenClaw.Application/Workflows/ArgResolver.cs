using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Workflows;

/// <summary>
/// Resolves skill arguments based on priority:
/// 1. FilledValue - explicit value in workflow definition
/// 2. ConfigKey - workflow-level variables
/// 3. InputMapping - output from upstream node (nodeId.jsonPath)
/// 4. UserPreferenceKey - user preferences
/// </summary>
public class ArgResolver(
    IUserPreferenceRepository userPreferenceRepository,
    ILogger<ArgResolver> logger)
{
    public async Task<string> ResolveArgsJsonAsync(
        SkillNode node,
        Dictionary<string, object>? workflowVariables,
        Dictionary<string, string> nodeOutputs,
        Guid? userId,
        CancellationToken ct)
    {
        if (node.Args is null || node.Args.Count == 0)
        {
            return "{}";
        }

        var resolvedArgs = new Dictionary<string, object?>();

        foreach (var (paramName, argSource) in node.Args)
        {
            var value = await ResolveArgValueAsync(
                paramName,
                argSource,
                workflowVariables,
                nodeOutputs,
                userId,
                ct);

            if (value is not null)
            {
                resolvedArgs[paramName] = value;
            }
        }

        return JsonSerializer.Serialize(resolvedArgs, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }

    private async Task<object?> ResolveArgValueAsync(
        string paramName,
        ArgSource source,
        Dictionary<string, object>? workflowVariables,
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

        // Priority 2: ConfigKey (workflow variables)
        if (!string.IsNullOrEmpty(source.ConfigKey) && workflowVariables is not null)
        {
            if (workflowVariables.TryGetValue(source.ConfigKey, out var configValue))
            {
                logger.LogDebug("Resolved {Param} from ConfigKey {Key}: {Value}",
                    paramName, source.ConfigKey, configValue);
                return configValue;
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

        logger.LogDebug("Could not resolve {Param}, returning null", paramName);
        return null;
    }

    private object? ResolveInputMapping(string mapping, Dictionary<string, string> nodeOutputs)
    {
        // Format: "nodeId.jsonPath" or just "nodeId" for full output
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
            // Return full output
            return ParseJsonValue(outputJson);
        }

        // Navigate JSON path (simple dot notation)
        try
        {
            var node = JsonNode.Parse(outputJson);
            if (node is null) return null;

            var pathParts = jsonPath.Split('.');
            foreach (var part in pathParts)
            {
                // Handle array indexing: items[0]
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

        // Try to parse as JSON
        try
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            {
                return JsonSerializer.Deserialize<object>(value);
            }

            // Try parse as number
            if (int.TryParse(value, out var intVal)) return intVal;
            if (double.TryParse(value, out var doubleVal)) return doubleVal;
            if (bool.TryParse(value, out var boolVal)) return boolVal;

            // Return as string
            return value;
        }
        catch
        {
            return value;
        }
    }
}
