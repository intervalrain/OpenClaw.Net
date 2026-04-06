using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Llm;

namespace OpenClaw.Application.Agents;

/// <summary>
/// Retries a single LLM streaming call on transient errors with exponential backoff.
/// Used by AgentPipeline's streaming path where middleware doesn't apply.
/// </summary>
public static class LlmRetryHelper
{
    public static async IAsyncEnumerable<ChatResponseChunk> StreamWithRetryAsync(
        ILlmProvider provider,
        IReadOnlyList<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? tools,
        ILogger logger,
        int maxRetries = 2,
        double baseDelaySeconds = 1.0,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            bool started = false;
            Exception? error = null;

            var enumerator = provider.ChatStreamAsync(messages, tools, ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    ChatResponseChunk current;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                            yield break; // Stream completed successfully
                        current = enumerator.Current;
                        started = true;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw; // User cancellation
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                        break; // Exit inner loop to retry
                    }

                    yield return current;
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            // If we got here with no error, stream completed
            if (error is null) yield break;

            // If we already started yielding content, we can't retry (partial data sent)
            // Only retry if the error happened before any content was emitted
            var category = LlmErrorClassifier.Classify(error);

            if (started || !LlmErrorClassifier.IsRetryable(category) || attempt == maxRetries)
            {
                logger.LogWarning(error,
                    "[StreamRetry] Failed. Category={Category}, Started={Started}, Attempt={Attempt}/{Max}",
                    category, started, attempt + 1, maxRetries + 1);
                throw error;
            }

            var delay = TimeSpan.FromSeconds(baseDelaySeconds * Math.Pow(2, attempt));
            logger.LogWarning(
                "[StreamRetry] {Category}, retrying in {Delay}s (attempt {Attempt}/{Max}): {Message}",
                category, delay.TotalSeconds, attempt + 1, maxRetries + 1, error.Message);

            await Task.Delay(delay, ct);
        }
    }
}
