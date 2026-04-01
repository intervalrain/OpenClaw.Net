namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// Represents the tracing context for distributed tracing.
/// Implements IAuditContext for use across MVC and NATS EventControllers.
/// </summary>
public record TraceContext : IAuditContext
{
    /// <summary>
    /// Trace ID propagated through the entire request chain.
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// Unique ID for this specific request.
    /// </summary>
    public required string RequestId { get; init; }

    public long Timestamp { get; init; }

    public static TraceContext Create(string? existingTraceId = null)
    {
        return new TraceContext
        {
            TraceId = existingTraceId ?? TraceIdGenerator.Generate(),
            RequestId = RequestIdGenerator.Generate(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }
}