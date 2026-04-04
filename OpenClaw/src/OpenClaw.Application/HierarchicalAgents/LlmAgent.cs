using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Base class for agents that wrap an LLM call with a tool-use loop.
/// Subclasses provide the system prompt and available tools.
/// </summary>
public abstract class LlmAgent : AgentBase
{
    public override AgentExecutionType ExecutionType => AgentExecutionType.Llm;

    /// <summary>System prompt for the LLM.</summary>
    protected abstract string SystemPrompt { get; }

    /// <summary>Tool names this agent can use (resolved from IToolRegistry).</summary>
    protected virtual IReadOnlyList<string> ToolNames => [];

    protected override async Task<AgentResult> ExecuteCoreAsync(AgentExecutionContext context, CancellationToken ct)
    {
        var providerFactory = context.Services.GetRequiredService<ILlmProviderFactory>();
        var toolRegistry = context.Services.GetRequiredService<IToolRegistry>();

        var provider = await ResolveProviderAsync(providerFactory, context, ct);

        var tools = ResolveTools(toolRegistry);
        var toolDefinitions = tools
            .Select(t => new ToolDefinition(t.Name, t.Description, t.Parameters))
            .ToList();

        var enrichedPrompt = await PreferenceInjector.EnrichWithPreferencesAsync(SystemPrompt, context, ct);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, enrichedPrompt),
            new(ChatRole.User, context.Input.RootElement.ToString())
        };

        decimal totalTokens = 0;

        for (var i = 0; i < context.Options.MaxIterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            var response = await provider.ChatAsync(
                messages,
                toolDefinitions.Count > 0 ? toolDefinitions : null,
                ct);

            if (!response.HasToolCalls)
            {
                var output = JsonDocument.Parse(
                    JsonSerializer.Serialize(new { content = response.Content }));
                return AgentResult.Success(output, totalTokens);
            }

            messages.Add(new ChatMessage(ChatRole.Assistant, response.Content, ToolCalls: response.ToolCalls));

            foreach (var toolCall in response.ToolCalls!)
            {
                var tool = tools.FirstOrDefault(t => t.Name == toolCall.Name);
                if (tool is null)
                {
                    messages.Add(new ChatMessage(ChatRole.Tool, $"Unknown tool: {toolCall.Name}", toolCall.Id));
                    continue;
                }

                var result = await tool.ExecuteAsync(new ToolContext(toolCall.Arguments), ct);
                messages.Add(new ChatMessage(
                    ChatRole.Tool,
                    result.IsSuccess ? result.Output : $"Error: {result.Error}",
                    toolCall.Id));
            }
        }

        return AgentResult.Failed($"Max iterations ({context.Options.MaxIterations}) reached.");
    }

    /// <summary>
    /// Resolves the LLM provider based on PreferredProvider and UserId.
    /// Priority: PreferredProvider with UserId > UserId default > global default.
    /// </summary>
    protected async Task<ILlmProvider> ResolveProviderAsync(
        ILlmProviderFactory factory, AgentExecutionContext context, CancellationToken ct)
    {
        if (context.UserId.HasValue)
            return await factory.GetProviderAsync(context.UserId.Value, PreferredProvider, ct);

        // No user context — fall back to global provider
        return await factory.GetProviderAsync(ct);
    }

    private List<IAgentTool> ResolveTools(IToolRegistry registry)
    {
        var tools = new List<IAgentTool>();
        foreach (var name in ToolNames)
        {
            var tool = registry.GetSkill(name);
            if (tool is not null)
                tools.Add(tool);
        }
        return tools;
    }
}
