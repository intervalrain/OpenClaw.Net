using System.Diagnostics;

using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents.Middlewares;

public class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : IAgentMiddleware
{
    public async Task<string> InvokeAsync(AgentContext context, AgentDelegate next, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        logger.LogInformation("[Agent] Input: {UserInput}", context.UserInput);

        try
        {
            var result = await next(context, ct);
            sw.Stop();
            logger.LogInformation("[Agent] Output: {Result} (Elapsed: {ElapsedInMs})", result, sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError("[Agent] Error: {Message} (Elapsed: {ElapsedInMs})", ex.Message, sw.ElapsedMilliseconds);
            throw;
        }
    }
}