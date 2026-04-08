using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Agents;

public interface IAgentPipeline
{
    Task<string> ExecuteAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        string? language = null,
        IReadOnlyList<ImageContent>? images = null,
        Guid? userId = null,
        Guid? workspaceId = null,
        IReadOnlyList<string>? userRoles = null,
        CancellationToken ct = default);

    IAsyncEnumerable<AgentStreamEvent> ExecuteStreamAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        string? language = null,
        IReadOnlyList<ImageContent>? images = null,
        Guid? userId = null,
        Guid? workspaceId = null,
        IReadOnlyList<string>? userRoles = null,
        IReadOnlyList<string>? priorityToolNames = null,
        CancellationToken ct = default);
}
