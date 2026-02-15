namespace OpenClaw.Contracts.Agents;

public interface IAgentPipeline
{
    Task<string> ExecuteAsync(string userInput, CancellationToken ct = default);
}