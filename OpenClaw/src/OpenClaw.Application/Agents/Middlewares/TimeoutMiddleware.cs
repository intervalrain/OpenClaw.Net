using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents.Middlewares;

public class TimeoutMiddleware(ILogger<TimeoutMiddleware> logger, TimeSpan? timeout = null) : IAgentMiddleware
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(2);

    public async Task<string> InvokeAsync(AgentContext context, AgentDelegate next, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeout);

        try
        {
            return await next(context, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("[Agent] Request timed out after {Timeout}", _timeout);
            throw new TimeoutException($"Agent request timed out after {_timeout}");
        }
    }
}