using ClawOS.Contracts.Llm;
using ClawOS.Contracts.Skills;

namespace ClawOS.Contracts.Agents;

public interface IAgentPipeline
{
    Task<string> ExecuteAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        string? language = null,
        IReadOnlyList<ImageContent>? images = null,
        Guid? userId = null,
        CancellationToken ct = default);

    IAsyncEnumerable<AgentStreamEvent> ExecuteStreamAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        string? language = null,
        IReadOnlyList<ImageContent>? images = null,
        Guid? userId = null,
        CancellationToken ct = default);
}