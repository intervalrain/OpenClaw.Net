namespace OpenClaw.Contracts.Llm;

public record ToolDefinition(string Name, string Description, object? Parameters = null);