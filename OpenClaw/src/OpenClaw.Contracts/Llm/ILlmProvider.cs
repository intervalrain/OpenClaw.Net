namespace OpenClaw.Contracts.Llm;

public interface ILlmProvider
{
    string Name { get; }

    Task<LlmChatResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        CancellationToken ct = default);

    IAsyncEnumerable<ChatResponseChunk> ChatStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        CancellationToken ct = default);
}