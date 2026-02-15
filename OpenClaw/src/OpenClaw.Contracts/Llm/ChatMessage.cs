namespace OpenClaw.Contracts.Llm;

public record ChatMessage(
    ChatRole Role,
    string? Content,
    string? ToolCallId = null,
    IReadOnlyList<ToolCall>? ToolCalls = null);