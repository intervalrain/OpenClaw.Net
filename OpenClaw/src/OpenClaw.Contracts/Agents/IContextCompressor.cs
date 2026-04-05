using OpenClaw.Contracts.Llm;

namespace OpenClaw.Contracts.Agents;

/// <summary>
/// Compresses conversation history when it exceeds token limits.
/// Implementations should preserve key decisions, tool results, and context
/// while reducing overall token count.
/// </summary>
public interface IContextCompressor
{
    /// <summary>
    /// Evaluates and compresses the message history if needed.
    /// Returns the (possibly shortened) message list.
    /// </summary>
    Task<List<ChatMessage>> CompressIfNeededAsync(
        List<ChatMessage> messages,
        ILlmProvider llmProvider,
        ContextCompressorOptions? options = null,
        CancellationToken ct = default);
}

public class ContextCompressorOptions
{
    /// <summary>
    /// Compression triggers when estimated tokens exceed this fraction of the
    /// provider's MaxContextTokens. Default 0.75 = compress at 75% usage.
    /// </summary>
    public double CompressAtPercentage { get; set; } = 0.75;

    /// <summary>
    /// Number of recent messages to keep verbatim (not summarized).
    /// </summary>
    public int RecentMessagesToKeep { get; set; } = 6;
}
