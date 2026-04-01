namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// NATS header keys for audit and tracing context.
/// </summary>
public class TraceConstants
{
    /// <summary>
    /// Propagated through entire request chain. Create new if not present.
    /// </summary>
    public const string TraceIdHeader = "X-Trace-Id";

    /// <summary>
    /// Generated new for each request
    /// </summary>
    public const string RequestIdHeader = "X-Request-Id";

    /// <summary>
    /// Send timestamp in unix milliseconds.
    /// </summary>
    public const string TimestampHeader = "X-Timestamp";
}