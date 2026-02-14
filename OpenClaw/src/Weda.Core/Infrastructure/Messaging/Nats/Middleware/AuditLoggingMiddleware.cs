using Microsoft.Extensions.Logging;
using Weda.Core.Infrastructure.Audit;

namespace Weda.Core.Infrastructure.Messaging.Nats.Middleware;

/// <summary>
/// Middleware that adds audit context (TraceId, RequestId) to the logging scope
/// for all EventController method invocations.
/// </summary>
public class AuditLoggingMiddleware(ILogger<AuditLoggingMiddleware> logger) : IEventControllerMiddleware
{
    public async Task InvokeAsync(EventControllerContext context, Func<Task> next)
    {
        var auditContext = context.AuditContext;
        var traceId = auditContext?.TraceId ?? "N/A";
        var requestId = auditContext?.RequestId ?? "N/A";

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["RequestId"] = requestId
        });


        logger.LogInformation(
            "[TraceId:{TraceId}] [RequestId:{RequestId}] Processing {Controller}.{Method} on subject {Subject}",
            traceId,
            requestId,
            context.Endpoint.ControllerType.Name,
            context.Endpoint.Method.Name,
            context.Subject);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await next();

            stopwatch.Stop();
            logger.LogInformation(
                "Completed {Controller}.{Method} in {ElapsedMs}ms",
                context.Endpoint.ControllerType.Name,
                context.Endpoint.Method.Name,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Failed {Controller}.{Method} after {ElapsedMs}ms",
                context.Endpoint.ControllerType.Name,
                context.Endpoint.Method.Name,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
