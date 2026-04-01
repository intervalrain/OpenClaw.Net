using NATS.Client.Core;

namespace Weda.Core.Infrastructure.Audit;

/// <summary>
/// Extension methods for NatsHeaders to handle trace context.
/// </summary>
public static class NatsHeadersExtensions
{
    /// <summary>
    /// Extracts TraceContext from NatsHeaders.
    /// Creates new IDs if not present.
    /// </summary>
    public static TraceContext GetTraceContext(this NatsHeaders? headers)
    {
        string? traceId = null;
        string? requestId = null;
        long timestamp = 0;

        if (headers is not null)
        {
            if (headers.TryGetValue(TraceConstants.TraceIdHeader, out var traceValues))
                traceId = traceValues.FirstOrDefault();
            
            if (headers.TryGetValue(TraceConstants.RequestIdHeader, out var requestValues))
                requestId = requestValues.FirstOrDefault();
            
            if (headers.TryGetValue(TraceConstants.TimestampHeader, out var tsValues) && long.TryParse(tsValues.FirstOrDefault(), out var ts))
                timestamp = ts;
        }

        return new TraceContext
        {
            TraceId = traceId ?? TraceIdGenerator.Generate(),
            RequestId = requestId ?? RequestIdGenerator.Generate(),
            Timestamp = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// Sets TraceContext values into NatsHeaders.
    /// </summary>
    public static NatsHeaders WithTraceContext(this NatsHeaders headers, TraceContext context)
    {
        headers[TraceConstants.TraceIdHeader] = context.TraceId;
        headers[TraceConstants.RequestIdHeader] = context.RequestId;
        headers[TraceConstants.TimestampHeader] = context.Timestamp.ToString();

        return headers;
    }

    /// <summary>
    /// Creates new NatsHeaders with TraceContext.
    /// </summary>
    public static NatsHeaders CreateWithTraceContext(TraceContext context)
    {
        var headers = new NatsHeaders();
        return headers.WithTraceContext(context);
    }    
}