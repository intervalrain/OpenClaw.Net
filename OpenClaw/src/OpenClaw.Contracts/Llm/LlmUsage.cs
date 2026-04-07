namespace OpenClaw.Contracts.Llm;

/// <summary>
/// Token usage statistics from a single LLM API call.
/// </summary>
public record LlmUsage(
    int InputTokens = 0,
    int OutputTokens = 0,
    int? CacheReadTokens = null,
    int? CacheCreationTokens = null)
{
    public int TotalTokens => InputTokens + OutputTokens;

    public static LlmUsage operator +(LlmUsage a, LlmUsage b) => new(
        a.InputTokens + b.InputTokens,
        a.OutputTokens + b.OutputTokens,
        (a.CacheReadTokens ?? 0) + (b.CacheReadTokens ?? 0),
        (a.CacheCreationTokens ?? 0) + (b.CacheCreationTokens ?? 0));

    public static readonly LlmUsage Empty = new();
}
