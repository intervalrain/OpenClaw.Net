namespace OpenClaw.Contracts.Llm;

public record ChatResponseChunk(
    string? ContentDelta = null,
    ToolCall? ToolCall = null,
    bool IsComplete = false);