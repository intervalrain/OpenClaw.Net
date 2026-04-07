namespace OpenClaw.Contracts.Llm;

/// <summary>
/// Categorized LLM API errors.
/// Ref: Claude Code services/api/errors.ts — 8 error categories with retry strategy.
/// </summary>
public enum LlmErrorCategory
{
    /// <summary>Rate limited (429). Retryable with backoff.</summary>
    RateLimited,

    /// <summary>Server overloaded (529, 503). Retryable with backoff.</summary>
    Overloaded,

    /// <summary>Transient server error (500, 502). Retryable with backoff.</summary>
    ServerError,

    /// <summary>Network/connection issue. Retryable with backoff.</summary>
    ConnectionError,

    /// <summary>Request too large for context window. Not retryable as-is.</summary>
    ContextOverflow,

    /// <summary>Authentication failure (401, 403). Not retryable.</summary>
    AuthError,

    /// <summary>Invalid request (400). Not retryable.</summary>
    InvalidRequest,

    /// <summary>Unknown/unclassified error.</summary>
    Unknown
}

public static class LlmErrorClassifier
{
    public static LlmErrorCategory Classify(Exception ex) => ex switch
    {
        HttpRequestException http => ClassifyHttp(http),
        TaskCanceledException => LlmErrorCategory.ConnectionError,
        TimeoutException => LlmErrorCategory.ConnectionError,
        _ when ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase) => LlmErrorCategory.RateLimited,
        _ when ex.Message.Contains("context length", StringComparison.OrdinalIgnoreCase) => LlmErrorCategory.ContextOverflow,
        _ when ex.Message.Contains("maximum context", StringComparison.OrdinalIgnoreCase) => LlmErrorCategory.ContextOverflow,
        _ => LlmErrorCategory.Unknown
    };

    public static bool IsRetryable(LlmErrorCategory category) => category switch
    {
        LlmErrorCategory.RateLimited => true,
        LlmErrorCategory.Overloaded => true,
        LlmErrorCategory.ServerError => true,
        LlmErrorCategory.ConnectionError => true,
        _ => false
    };

    private static LlmErrorCategory ClassifyHttp(HttpRequestException http)
    {
        return http.StatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => LlmErrorCategory.RateLimited,
            System.Net.HttpStatusCode.ServiceUnavailable => LlmErrorCategory.Overloaded,
            System.Net.HttpStatusCode.BadGateway => LlmErrorCategory.ServerError,
            System.Net.HttpStatusCode.InternalServerError => LlmErrorCategory.ServerError,
            System.Net.HttpStatusCode.GatewayTimeout => LlmErrorCategory.ConnectionError,
            System.Net.HttpStatusCode.Unauthorized => LlmErrorCategory.AuthError,
            System.Net.HttpStatusCode.Forbidden => LlmErrorCategory.AuthError,
            System.Net.HttpStatusCode.BadRequest => ClassifyBadRequest(http),
            _ => LlmErrorCategory.Unknown
        };
    }

    private static LlmErrorCategory ClassifyBadRequest(HttpRequestException http)
    {
        var msg = http.Message;
        if (msg.Contains("context length", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("maximum context", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("too many tokens", StringComparison.OrdinalIgnoreCase))
            return LlmErrorCategory.ContextOverflow;

        return LlmErrorCategory.InvalidRequest;
    }
}
