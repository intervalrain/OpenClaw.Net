using System.Text.Json;
using System.Text.Json.Serialization;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Agents;

/// <summary>
/// Validates that a JSON string conforms to a given JSON Schema.
/// Used to enforce structured output contracts between agents or
/// between DAG workflow nodes.
///
/// Ref: Claude Code SyntheticOutputTool — validates LLM output against
/// JSON schema with detailed path errors.
/// </summary>
public class StructuredOutputTool : IAgentTool
{
    public string Name => "validate_json";

    public string Description =>
        "Validate a JSON string against a JSON Schema. Returns validation result with errors. " +
        "Use this to ensure structured output conforms to a contract before passing to the next step.";

    public object? Parameters => new ToolParameters
    {
        Properties = new Dictionary<string, ToolProperty>
        {
            ["json"] = new() { Type = "string", Description = "The JSON string to validate" },
            ["schema"] = new() { Type = "string", Description = "The JSON Schema to validate against (as a JSON string)" }
        },
        Required = ["json", "schema"]
    };

    public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
    {
        var args = JsonSerializer.Deserialize<ValidateArgs>(context.Arguments ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrWhiteSpace(args?.Json))
            return Task.FromResult(ToolResult.Failure("json parameter is required"));
        if (string.IsNullOrWhiteSpace(args?.Schema))
            return Task.FromResult(ToolResult.Failure("schema parameter is required"));

        try
        {
            // Parse both JSON and schema
            using var jsonDoc = JsonDocument.Parse(args.Json);
            using var schemaDoc = JsonDocument.Parse(args.Schema);

            var errors = new List<string>();
            ValidateElement(jsonDoc.RootElement, schemaDoc.RootElement, "$", errors);

            if (errors.Count == 0)
                return Task.FromResult(ToolResult.Success("Validation PASSED. JSON conforms to schema."));

            var errorList = string.Join("\n", errors.Select((e, i) => $"  {i + 1}. {e}"));
            return Task.FromResult(ToolResult.Failure($"Validation FAILED with {errors.Count} error(s):\n{errorList}"));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(ToolResult.Failure($"Invalid JSON: {ex.Message}"));
        }
    }

    private static void ValidateElement(JsonElement value, JsonElement schema, string path, List<string> errors)
    {
        // Check type constraint
        if (schema.TryGetProperty("type", out var typeProp))
        {
            var expectedType = typeProp.GetString();
            var actualType = value.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => "array",
                JsonValueKind.String => "string",
                JsonValueKind.Number => value.ToString()?.Contains('.') == true ? "number" : "integer",
                JsonValueKind.True or JsonValueKind.False => "boolean",
                JsonValueKind.Null => "null",
                _ => "unknown"
            };

            // "number" accepts "integer" too
            if (expectedType != actualType && !(expectedType == "number" && actualType == "integer"))
            {
                errors.Add($"{path}: expected type '{expectedType}' but got '{actualType}'");
                return;
            }
        }

        // Check required properties
        if (value.ValueKind == JsonValueKind.Object && schema.TryGetProperty("required", out var required))
        {
            foreach (var req in required.EnumerateArray())
            {
                var propName = req.GetString()!;
                if (!value.TryGetProperty(propName, out _))
                {
                    errors.Add($"{path}: missing required property '{propName}'");
                }
            }
        }

        // Recurse into object properties
        if (value.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties))
        {
            foreach (var prop in properties.EnumerateObject())
            {
                if (value.TryGetProperty(prop.Name, out var childValue))
                {
                    ValidateElement(childValue, prop.Value, $"{path}.{prop.Name}", errors);
                }
            }
        }

        // Recurse into array items
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            int i = 0;
            foreach (var item in value.EnumerateArray())
            {
                ValidateElement(item, items, $"{path}[{i}]", errors);
                i++;
            }
        }
    }

    private record ValidateArgs
    {
        [JsonPropertyName("json")] public string? Json { get; init; }
        [JsonPropertyName("schema")] public string? Schema { get; init; }
    }
}
