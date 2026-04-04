using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// An IAgent backed by an AGENT.md definition file.
/// For LLM/Hybrid types, executes an LLM tool-use loop using the agent's instructions and tools.
/// For Deterministic type, returns the instructions as output (useful for template agents).
/// </summary>
public class FileDefinedAgent : AgentBase
{
    private readonly AgentDefinition _definition;

    public FileDefinedAgent(AgentDefinition definition)
    {
        _definition = definition;
    }

    public override string Name => _definition.Name;
    public override string Description => _definition.Description;
    public override string Version => _definition.Version;
    public override AgentExecutionType ExecutionType => _definition.ExecutionType;
    public override string? PreferredProvider => _definition.PreferredProvider;

    protected override async Task<AgentResult> ExecuteCoreAsync(
        AgentExecutionContext context, CancellationToken ct)
    {
        if (_definition.ExecutionType == AgentExecutionType.Deterministic)
            return ExecuteDeterministic(context);

        return await ExecuteWithLlmAsync(context, ct);
    }

    private AgentResult ExecuteDeterministic(AgentExecutionContext context)
    {
        var output = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            instructions = _definition.Instructions,
            input = context.Input.RootElement.ToString()
        }));
        return AgentResult.Success(output);
    }

    private async Task<AgentResult> ExecuteWithLlmAsync(
        AgentExecutionContext context, CancellationToken ct)
    {
        var providerFactory = context.Services.GetRequiredService<ILlmProviderFactory>();
        var toolRegistry = context.Services.GetRequiredService<IToolRegistry>();

        // Resolve provider using PreferredProvider from AGENT.md + user context
        var provider = context.UserId.HasValue
            ? await providerFactory.GetProviderAsync(context.UserId.Value, PreferredProvider, ct)
            : await providerFactory.GetProviderAsync(ct);

        // Resolve tools
        var tools = new List<IAgentTool>();
        foreach (var toolName in _definition.Tools)
        {
            var tool = toolRegistry.GetSkill(toolName);
            if (tool is not null)
                tools.Add(tool);
        }

        // If the agent has a scripts/ directory, make run_script available
        if (_definition.Scripts is { Count: > 0 })
        {
            var runScriptTool = toolRegistry.GetSkill("run_script");
            if (runScriptTool is not null && tools.All(t => t.Name != "run_script"))
                tools.Add(runScriptTool);
        }

        var toolDefinitions = tools
            .Select(t => new ToolDefinition(t.Name, t.Description, t.Parameters))
            .ToList();

        // Build instructions with any directory path resolution
        var instructions = _definition.Instructions;
        if (_definition.DirectoryPath is not null)
            instructions = instructions.Replace("{AGENT_DIR}", _definition.DirectoryPath);

        instructions = await PreferenceInjector.EnrichWithPreferencesAsync(instructions, context, ct);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, instructions),
            new(ChatRole.User, context.Input.RootElement.ToString())
        };

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
                return AgentResult.Success(output);
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
}
