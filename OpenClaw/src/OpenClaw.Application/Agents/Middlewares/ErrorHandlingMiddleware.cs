using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents.Middlewares;

public class ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger) : IAgentMiddleware
{
    public async Task<string> InvokeAsync(AgentContext context, AgentDelegate next, CancellationToken ct = default)
    {
        try
        {
            return await next(context, ct);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("[Agent] Operation cancelled");
            return "Operation was cancelled";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Agent] Unhandled exception: {Message}", ex.Message);
            return $"An error occurred: {ex.Message}";
        }
    }
}