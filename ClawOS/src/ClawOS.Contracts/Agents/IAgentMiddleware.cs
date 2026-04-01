namespace ClawOS.Contracts.Agents;

public interface IAgentMiddleware
{
    Task<string> InvokeAsync(AgentContext context, AgentDelegate next, CancellationToken ct = default);
}