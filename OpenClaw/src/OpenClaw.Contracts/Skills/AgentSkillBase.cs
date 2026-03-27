using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using OpenClaw.Contracts.Llm;

namespace OpenClaw.Contracts.Skills;

public abstract class AgentToolBase<TArgs> : IAgentTool where TArgs : class
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public object? Parameters => GenerateToolParameters();

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<TArgs>(context.Arguments ?? "{}", new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args is null)
            {
                return ToolResult.Failure("Failed to parse arguments.");
            }

            return await ExecuteAsync(args, ct);
        }
        catch (JsonException ex)
        {
            return ToolResult.Failure($"Invalid arguments: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ToolResult.Failure(ex.Message);
        }
    }

    public abstract Task<ToolResult> ExecuteAsync(TArgs args, CancellationToken ct);

    private static ToolParameters GenerateToolParameters()
    {
        var properties = new Dictionary<string, ToolProperty>();
        var required = new List<string>();
        var nullabilityContext = new NullabilityInfoContext();

        foreach (var prop in typeof(TArgs).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var toolProperty = new ToolProperty
            {
                Type = GetJsonType(prop.PropertyType),
                Description = prop.GetCustomAttribute<DescriptionAttribute>()?.Description,
                DefaultKey = prop.GetCustomAttribute<DefaultFromAttribute>()?.Key
            };

            properties[ToCamelCase(prop.Name)] = toolProperty;

            if (!IsNullable(prop, nullabilityContext))
            {
                required.Add(ToCamelCase(prop.Name));
            }
        }

        return new ToolParameters
        {
            Type = "object",
            Properties = properties,
            Required = required
        };
    }

    private static string GetJsonType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType switch
        {
            Type t when t == typeof(string) => "string",
            Type t when t == typeof(int) || t == typeof(long) || t == typeof(short) => "integer",
            Type t when t == typeof(float) || t == typeof(double) || t == typeof(decimal) => "number",
            Type t when t == typeof(bool) => "boolean",
            Type t when t.IsArray => "array",
            Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) => "array",
            _ => "string"
        };
    }

    private static bool IsNullable(PropertyInfo prop, NullabilityInfoContext context)
    {
        // For value types, check Nullable<T>
        if (prop.PropertyType.IsValueType)
            return Nullable.GetUnderlyingType(prop.PropertyType) != null;

        // For reference types, use NullabilityInfoContext to respect C# nullable annotations
        var nullabilityInfo = context.Create(prop);
        return nullabilityInfo.WriteState == NullabilityState.Nullable;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}