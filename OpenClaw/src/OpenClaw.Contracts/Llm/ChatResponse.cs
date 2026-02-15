namespace OpenClaw.Contracts.Llm;

public record ChatResponse(string? Content, IReadOnlyList<ToolCall>? ToolCalls = null)
{
    public bool HasToolCalls => ToolCalls is { Count: > 0 };
}