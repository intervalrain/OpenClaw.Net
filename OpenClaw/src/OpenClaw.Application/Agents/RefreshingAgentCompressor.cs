using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.Agents;

/// <summary>
/// Compresses conversation history by spawning a fresh "refreshing agent" — an independent
/// LLM call that summarizes the old messages from an unbiased perspective.
/// This avoids the original agent's bias toward its own reasoning.
///
/// Ref: Claude Code compact.ts — multi-stage compaction with boundary markers.
/// Our approach goes further: a brand-new agent produces the summary, not the original.
/// </summary>
public class RefreshingAgentCompressor(ILogger<RefreshingAgentCompressor> logger) : IContextCompressor
{
    private const string SummarySystemPrompt = """
        You are a conversation summarizer. Your job is to produce a structured summary
        of the conversation history provided below.

        Your summary MUST preserve:
        - Key decisions made and their rationale
        - Important tool execution results (success/failure, outputs)
        - Unresolved questions or pending tasks
        - Critical context that would be needed to continue the conversation

        Your summary MUST NOT include:
        - Redundant back-and-forth or pleasantries
        - Intermediate reasoning that led to abandoned approaches
        - Verbose tool arguments or raw output (summarize the outcome instead)

        Format: Use structured bullet points grouped by topic.
        Length: Keep under 800 tokens.
        Language: Use the same language as the conversation.
        """;

    public async Task<List<ChatMessage>> CompressIfNeededAsync(
        List<ChatMessage> messages,
        ILlmProvider llmProvider,
        ContextCompressorOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ContextCompressorOptions();

        // Skip system messages when counting
        var nonSystemMessages = messages.Where(m => m.Role != ChatRole.System).ToList();

        if (nonSystemMessages.Count <= options.RecentMessagesToKeep)
            return messages;

        // Estimate tokens (rough: 1 token ≈ 2 chars for mixed CJK/English)
        var totalChars = nonSystemMessages.Sum(m => m.Content?.Length ?? 0);
        var estimatedTokens = totalChars / 2;

        // Threshold = provider's context window × compression percentage
        var threshold = (int)(llmProvider.MaxContextTokens * options.CompressAtPercentage);

        if (estimatedTokens <= threshold)
            return messages;

        logger.LogInformation(
            "Context compression triggered: ~{EstimatedTokens} tokens from {MessageCount} messages " +
            "(threshold: {Threshold}, model context: {ContextWindow})",
            estimatedTokens, nonSystemMessages.Count, threshold, llmProvider.MaxContextTokens);

        // Preserve system messages (they go first)
        var systemMessages = messages.Where(m => m.Role == ChatRole.System).ToList();

        // Split: old messages to summarize vs recent to keep verbatim
        var oldMessages = nonSystemMessages
            .Take(nonSystemMessages.Count - options.RecentMessagesToKeep)
            .ToList();
        var recentMessages = nonSystemMessages
            .Skip(nonSystemMessages.Count - options.RecentMessagesToKeep)
            .ToList();

        // Spawn a fresh agent call to summarize (the "refreshing" approach)
        var summary = await SummarizeWithFreshAgentAsync(oldMessages, llmProvider, ct);

        logger.LogInformation(
            "Compressed {OldCount} messages into summary ({SummaryLength} chars), keeping {RecentCount} recent",
            oldMessages.Count, summary.Length, recentMessages.Count);

        // Reassemble: system prompts + summary boundary + recent messages
        var compacted = new List<ChatMessage>();
        compacted.AddRange(systemMessages);
        compacted.Add(new ChatMessage(
            ChatRole.System,
            $"[Conversation Summary — the following is a compressed summary of earlier messages]\n\n{summary}"));
        compacted.AddRange(recentMessages);

        return compacted;
    }

    private async Task<string> SummarizeWithFreshAgentAsync(
        List<ChatMessage> oldMessages,
        ILlmProvider llmProvider,
        CancellationToken ct)
    {
        // Format the conversation for the summarizer
        var conversationText = string.Join("\n", oldMessages.Select(m =>
        {
            var role = m.Role switch
            {
                ChatRole.User => "User",
                ChatRole.Assistant => "Assistant",
                ChatRole.Tool => $"Tool({m.ToolCallId})",
                _ => m.Role.ToString()
            };
            var content = m.Content ?? "";
            // Truncate very long tool results to avoid blowing up the summarizer's context
            if (m.Role == ChatRole.Tool && content.Length > 500)
                content = content[..500] + "... [truncated]";
            return $"[{role}]: {content}";
        }));

        var summaryMessages = new List<ChatMessage>
        {
            new(ChatRole.System, SummarySystemPrompt),
            new(ChatRole.User, $"Summarize this conversation:\n\n{conversationText}")
        };

        try
        {
            // Fresh LLM call — no tools, no history, clean context
            var response = await llmProvider.ChatAsync(summaryMessages, tools: null, ct);
            return response.Content ?? "Previous conversation context.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Refreshing agent summarization failed, falling back to truncation");
            // Fallback: keep first and last portions of conversation text
            return conversationText.Length > 1000
                ? conversationText[..500] + "\n\n[...truncated...]\n\n" + conversationText[^500..]
                : conversationText;
        }
    }
}
