namespace OpenClaw.Contracts.Llm;

public record LlmChatResponse(string? Content, IReadOnlyList<ToolCall>? ToolCalls = null)
{
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}