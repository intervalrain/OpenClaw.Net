namespace OpenClaw.Contracts.Llm;

/// <summary>
/// Tracks prompt cache behavior to detect and diagnose cache breaks.
/// Ref: Claude Code promptCacheBreakDetection.ts — two-phase tracking
/// with hash comparison and detailed analytics.
/// </summary>
public interface IPromptCacheTracker
{
    /// <summary>
    /// Records the prompt state before an LLM call (hashes system prompt, tools, etc.).
    /// </summary>
    void RecordPreCallState(string querySource, string systemPromptHash, string toolSchemaHash);

    /// <summary>
    /// Checks response for cache breaks by comparing cache_read_tokens against baseline.
    /// </summary>
    CacheBreakResult? CheckForCacheBreak(string querySource, LlmUsage usage);
}

public record CacheBreakResult(
    string QuerySource,
    int PreviousCacheReadTokens,
    int CurrentCacheReadTokens,
    string? PossibleCause);
