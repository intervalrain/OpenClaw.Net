namespace OpenClaw.Contracts.Llm;

public class ToolParameters
{
    public string Type { get; set; } = "object";
    public Dictionary<string, ToolProperty>? Properties { get; set; }
    public IEnumerable<string>? Required { get; set; }
}

public class ToolProperty
{
    public string? Type { get; set; }
    public string? Description { get; set; }
}