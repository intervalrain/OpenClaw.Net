namespace OpenClaw.Contracts.Llm;

public record LlmChatResponse(
    string? Content,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    LlmUsage? Usage = null)
{
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}