using OpenClaw.Contracts.Agents;

namespace OpenClaw.Application.Agents.ContextProviders;

/// <summary>
/// Composes system prompt from multiple IContextProvider instances.
/// Each provider contributes an independent block, ordered by Order property.
/// </summary>
public class SystemPromptAssembler(IEnumerable<IContextProvider> providers)
{
    public async Task<string> AssembleAsync(ContextProviderRequest request, CancellationToken ct = default)
    {
        var parts = new List<(int order, string content)>();

        foreach (var provider in providers)
        {
            var content = await provider.GetContextAsync(request, ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                parts.Add((provider.Order, content));
            }
        }

        return string.Join("\n\n", parts.OrderBy(p => p.order).Select(p => p.content));
    }
}
