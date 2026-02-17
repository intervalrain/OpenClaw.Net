using System.Runtime.CompilerServices;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.Agents;

public class AgentPipeline(
    ILlmProvider llmProvider,
    IEnumerable<IAgentSkill> skills,
    AgentPipelineOptions options,
    IReadOnlyList<IAgentMiddleware>? middlewares = null) : IAgentPipeline
{
    private readonly Dictionary<string, IAgentSkill> _skillMap = skills.ToDictionary(s => s.Name);
    private readonly IReadOnlyList<IAgentMiddleware> _middlewares = middlewares ?? [];

    public async Task<string> ExecuteAsync(string userInput, CancellationToken ct = default)
    {
        var context = new AgentContext
        {
            UserInput = userInput,
            LlmProvider = llmProvider,
            Skills = _skillMap.Values.ToList(),
            Options = options
        };

        if (options.SystemPrompt is not null)
        {
            context.Messages.Add(new ChatMessage(ChatRole.System, options.SystemPrompt));
        }

        var pipeline = BuildPipeline();
        return await pipeline(context, ct);
    }

    public async IAsyncEnumerable<AgentStreamEvent> ExecuteStreamAsync(
        string userInput,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>();

        if (options.SystemPrompt is not null)
        {
            messages.Add(new ChatMessage(ChatRole.System, options.SystemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, userInput));

        var toolDefinitions = _skillMap.Values
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        for (int i = 0; i < options.MaxIterations; i++)
        {
            yield return new AgentStreamEvent(AgentStreamEventType.Thinking);

            var contentBuilder = new System.Text.StringBuilder();
            var toolCalls = new List<ToolCall>();

            await foreach (var chunk in llmProvider.ChatStreamAsync(messages, toolDefinitions, ct))
            {
                if (chunk.ContentDelta is not null)
                {
                    contentBuilder.Append(chunk.ContentDelta);
                    yield return new AgentStreamEvent(AgentStreamEventType.ContentDelta, chunk.ContentDelta);
                }

                if (chunk.ToolCall is not null)
                {
                    toolCalls.Add(chunk.ToolCall);
                }
            }

            var content = contentBuilder.ToString();

            if (toolCalls.Count == 0)
            {
                messages.Add(new ChatMessage(ChatRole.Assistant, content));
                yield return new AgentStreamEvent(AgentStreamEventType.Completed, content);
                yield break;
            }

            messages.Add(new ChatMessage(ChatRole.Assistant, content, ToolCalls: toolCalls));

            foreach (var toolCall in toolCalls)
            {
                yield return new AgentStreamEvent(AgentStreamEventType.ToolExecuting, ToolName: toolCall.Name);

                var result = await ExecuteToolCallAsync(toolCall, ct);
                messages.Add(new ChatMessage(ChatRole.Tool, result, toolCall.Id));

                yield return new AgentStreamEvent(AgentStreamEventType.ToolCompleted, result, toolCall.Name);
            }
        }

        yield return new AgentStreamEvent(AgentStreamEventType.Error, "Max iteration reached");
    }

    private AgentDelegate BuildPipeline()
    {
        AgentDelegate core = ExecuteCoreAsync;

        for (int i = _middlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _middlewares[i];
            var next = core;
            core = (ctx, ct) => middleware.InvokeAsync(ctx, next, ct);
        }

        return core;
    }

    private async Task<string> ExecuteCoreAsync(AgentContext context, CancellationToken ct)
    {
        context.Messages.Add(new ChatMessage(ChatRole.User, context.UserInput));

        var toolDefinitions = _skillMap.Values
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        for (int i = 0; i < context.Options.MaxIterations; i++)
        {
            var response = await context.LlmProvider.ChatAsync(context.Messages, toolDefinitions, ct);

            if (!response.HasToolCalls)
            {
                context.Messages.Add(new ChatMessage(ChatRole.Assistant, response.Content ?? ""));
                return response.Content ?? string.Empty;
            }

            context.Messages.Add(new ChatMessage(ChatRole.Assistant, response.Content, ToolCalls: response.ToolCalls));

            foreach (var toolCall in response.ToolCalls!)
            {
                var result = await ExecuteToolCallAsync(toolCall, ct);
                context.Messages.Add(new ChatMessage(ChatRole.Tool, result, toolCall.Id));
            }
        }

        return "Max iteration reached";
    }

    private async Task<string> ExecuteToolCallAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (!_skillMap.TryGetValue(toolCall.Name, out var skill))
        {
            return $"Error: skill '{toolCall.Name}' not found.";
        }

        var skillContext = new SkillContext { Arguments = toolCall.Arguments };
        var result = await skill.ExecuteAsync(skillContext, ct);
        return result.IsSuccess ? result.Output ?? string.Empty : $"Error: {result.Error}";
    }
}