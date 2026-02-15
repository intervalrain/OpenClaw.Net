namespace OpenClaw.Contracts.Llm;

public record ToolCall(string Id, string Name, string Arguments);