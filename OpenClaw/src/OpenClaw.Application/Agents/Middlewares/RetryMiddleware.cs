using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;

namespace OpenClaw.Application.Agents.Middlewares;

/// <summary>
/// Retries the agent pipeline on transient LLM errors with exponential backoff.
///
/// Ref: Claude Code services/api/errors.ts — categorizes errors, retries with
/// progressive degradation (full retry → compact history → strip images → fail).
///
/// Retry policy:
///   - RateLimited / Overloaded / ServerError / ConnectionError → retry up to MaxRetries
///   - Exponential backoff: 1s, 2s, 4s (base × 2^attempt)
///   - AuthError / InvalidRequest / ContextOverflow → fail immediately
/// </summary>
public class RetryMiddleware(
    ILogger<RetryMiddleware> logger,
    int maxRetries = 3,
    double baseDelaySeconds = 1.0) : IAgentMiddleware
{
    public async Task<string> InvokeAsync(AgentContext context, AgentDelegate next, CancellationToken ct = default)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await next(context, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // User cancellation — don't retry
            }
            catch (Exception ex)
            {
                lastException = ex;
                var category = LlmErrorClassifier.Classify(ex);

                if (!LlmErrorClassifier.IsRetryable(category) || attempt == maxRetries)
                {
                    logger.LogWarning(ex,
                        "[Retry] Non-retryable or max retries reached. Category={Category}, Attempt={Attempt}/{Max}",
                        category, attempt + 1, maxRetries + 1);
                    throw;
                }

                var delay = TimeSpan.FromSeconds(baseDelaySeconds * Math.Pow(2, attempt));
                logger.LogWarning(
                    "[Retry] {Category} error, retrying in {Delay}s (attempt {Attempt}/{Max}): {Message}",
                    category, delay.TotalSeconds, attempt + 1, maxRetries + 1, ex.Message);

                await Task.Delay(delay, ct);
            }
        }

        // Should not reach here, but just in case
        throw lastException ?? new InvalidOperationException("Retry exhausted with no exception");
    }
}
