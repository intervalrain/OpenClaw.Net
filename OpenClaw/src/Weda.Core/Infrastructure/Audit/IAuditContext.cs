namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// Represents the audit context for distributed tracing.
/// Used by both MVC Controllers and EventControllers.
/// </summary>
public interface IAuditContext
{
    /// <summary>
    /// Trace ID that propagates through the entire request chain.
    /// Same across all services handling a single user request.
    /// </summary>
    string TraceId { get; }

    /// <summary>
    /// Request ID that is unique per hop/service call.
    /// New ID generated for each outbound message.
    /// </summary>
    string RequestId { get; }

    /// <summary>
    /// Timestamp when the context was created (Unix milliseconds).
    /// </summary>
    long Timestamp { get; }
}
