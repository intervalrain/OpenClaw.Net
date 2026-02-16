using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

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