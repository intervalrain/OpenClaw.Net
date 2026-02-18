using OpenClaw.Contracts.Llm;

namespace OpenClaw.Contracts.Agents;

public interface IAgentPipeline
{
    Task<string> ExecuteAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        CancellationToken ct = default);

    IAsyncEnumerable<AgentStreamEvent> ExecuteStreamAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        CancellationToken ct = default);
}