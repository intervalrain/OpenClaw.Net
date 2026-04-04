using System.Text.Json.Serialization;

namespace OpenClaw.Contracts.Llm;

public class ToolParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";
    
    [JsonPropertyName("properties")]
    public Dictionary<string, ToolProperty>? Properties { get; set; }
    
    [JsonPropertyName("required")]
    public IEnumerable<string>? Required { get; set; }
}

public class ToolProperty
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// For array types, defines the schema of each element.
    /// Required by OpenAI API when type is "array".
    /// </summary>
    [JsonPropertyName("items")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolProperty? Items { get; set; }

    /// <summary>
    /// ConfigStore or UserPreference key to look up a default value for this parameter.
    /// </summary>
    [JsonPropertyName("defaultKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultKey { get; set; }
}