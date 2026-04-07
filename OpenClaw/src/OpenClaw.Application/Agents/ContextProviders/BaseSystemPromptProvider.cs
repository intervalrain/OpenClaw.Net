using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents.ContextProviders;

/// <summary>
/// Provides the base system prompt from AgentPipelineOptions.
/// </summary>
public class BaseSystemPromptProvider(AgentPipelineOptions options) : IContextProvider
{
    public int Order => 0;

    public Task<string?> GetContextAsync(ContextProviderRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(options.SystemPrompt);
    }
}
