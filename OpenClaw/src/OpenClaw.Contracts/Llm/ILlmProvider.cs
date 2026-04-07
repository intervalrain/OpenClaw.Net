namespace OpenClaw.Contracts.Llm;

public interface ILlmProvider
{
    string Name { get; }

    /// <summary>
    /// Maximum context window size in tokens for the current model.
    /// Used by context compressor to decide when to trigger compression.
    /// </summary>
    int MaxContextTokens { get; }

    Task<LlmChatResponse> ChatAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        CancellationToken ct = default);

    IAsyncEnumerable<ChatResponseChunk> ChatStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools = null,
        CancellationToken ct = default);
}