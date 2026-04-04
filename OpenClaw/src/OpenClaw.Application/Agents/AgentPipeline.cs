using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.Agents;

public class AgentPipeline(
    ILlmProviderFactory llmProviderFactory,
    IEnumerable<IAgentTool> skills,
    AgentPipelineOptions options,
    ISkillStore? skillStore = null,
    IReadOnlyList<IAgentMiddleware>? middlewares = null) : IAgentPipeline
{
    private readonly Dictionary<string, IAgentTool> _skillMap = skills.ToDictionary(s => s.Name);
    private readonly IReadOnlyList<IAgentMiddleware> _middlewares = middlewares ?? [];

    public async Task<string> ExecuteAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        string? language = null,
        IReadOnlyList<ImageContent>? images = null,
        Guid? userId = null,
        Guid? workspaceId = null,
        CancellationToken ct = default)
    {
        var context = new AgentContext
        {
            UserInput = userInput,
            Images = images,
            LlmProvider = userId.HasValue
                ? await llmProviderFactory.GetProviderAsync(userId.Value, ct: ct)
                : await llmProviderFactory.GetProviderAsync(ct),
            Skills = _skillMap.Values.ToList(),
            Options = options,
            UserId = userId,
            WorkspaceId = workspaceId
        };


        var systemPrompt = BuildSystemPrompt(language);

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            context.Messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        // Add conversation history
        if (history is { Count: > 0 })
        {
            context.Messages.AddRange(history);
        }

        var pipeline = BuildPipeline();
        return await pipeline(context, ct);
    }

    public async IAsyncEnumerable<AgentStreamEvent> ExecuteStreamAsync(
        string userInput,
        IReadOnlyList<ChatMessage>? history = null,
        string? language = null,
        IReadOnlyList<ImageContent>? images = null,
        Guid? userId = null,
        Guid? workspaceId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>();

        var systemPrompt = BuildSystemPrompt(language);

        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        // Add conversation history
        if (history is { Count: > 0 })
        {
            messages.AddRange(history);
        }

        // Detect @skill mentions and augment system prompt + available tools
        var (processedInput, skillPrompt, extraTools) = ResolveSkillMentions(userInput);

        if (!string.IsNullOrEmpty(skillPrompt))
        {
            messages.Add(new ChatMessage(ChatRole.System, skillPrompt));
        }

        // Add user message with images if present
        messages.Add(new ChatMessage(ChatRole.User, processedInput, Images: images));

        var toolDefinitions = _skillMap.Values
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        // Add skill-specific tools that aren't already in the tool list
        foreach (var extraTool in extraTools)
        {
            if (!toolDefinitions.Any(t => t.Name == extraTool.Name))
            {
                toolDefinitions.Add(extraTool);
            }
        }

        var llmProvider = userId.HasValue
            ? await llmProviderFactory.GetProviderAsync(userId.Value, ct: ct)
            : await llmProviderFactory.GetProviderAsync(ct);

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

                var result = await ExecuteToolCallAsync(toolCall, userId, workspaceId, ct);
                messages.Add(new ChatMessage(ChatRole.Tool, result, toolCall.Id));

                yield return new AgentStreamEvent(AgentStreamEventType.ToolCompleted, result, toolCall.Name);
            }
        }

        yield return new AgentStreamEvent(AgentStreamEventType.Error, "Max iteration reached");
    }

    /// <summary>
    /// Detects @skill-name mentions in user input.
    /// Returns: (cleaned input, skill system prompt, extra tool definitions)
    /// </summary>
    private (string input, string? skillPrompt, List<ToolDefinition> extraTools) ResolveSkillMentions(string userInput)
    {
        if (skillStore is null)
        {
            System.Console.WriteLine("[ResolveSkillMentions] skillStore is NULL");
            return (userInput, null, []);
        }

        System.Console.WriteLine($"[ResolveSkillMentions] Available skills: {string.Join(", ", skillStore.GetAllSkills().Select(s => s.Name))}");

        var matches = Regex.Matches(userInput, @"@([\w-]+)");
        if (matches.Count == 0) return (userInput, null, []);

        var skillParts = new List<string>();
        var extraTools = new List<ToolDefinition>();

        foreach (Match match in matches)
        {
            var skillName = match.Groups[1].Value;
            var skill = skillStore.GetSkill(skillName);
            if (skill is null)
            {
                System.Console.WriteLine($"[ResolveSkillMentions] Skill '{skillName}' not found");
                continue;
            }
            System.Console.WriteLine($"[ResolveSkillMentions] Loaded skill '{skillName}' with {skill.Tools.Count} tools");

            // Resolve {SKILL_DIR} in instructions
            var instructions = skill.Instructions;
            if (skill is SkillDefinition sd && sd.DirectoryPath is not null)
            {
                instructions = instructions.Replace("{SKILL_DIR}", sd.DirectoryPath);
            }

            skillParts.Add($"## Skill: {skill.Name}\n{instructions}");

            // Add the skill's tools to available tools
            foreach (var toolName in skill.Tools)
            {
                if (_skillMap.TryGetValue(toolName, out var tool))
                {
                    extraTools.Add(new ToolDefinition(tool.Name, tool.Description, tool.Parameters));
                }
            }
        }

        var skillPrompt = skillParts.Count > 0
            ? "The user has requested the following skill(s). Follow their instructions carefully.\n\n" + string.Join("\n\n", skillParts)
            : null;

        return (userInput, skillPrompt, extraTools);
    }

    private string BuildSystemPrompt(string? language)
    {
        var parts = new List<string>();

        if (options.SystemPrompt is not null)
        {
            parts.Add(options.SystemPrompt);
        }

        parts.Add("If the user's request involves multiple coordinated steps or a complex multi-step task, " +
                   "use the `pioneer_plan` tool to decompose and execute it as a DAG workflow.");

        parts.Add("When a user asks you to set up a recurring or complex workflow, use the create_agent tool to create " +
                   "a reusable agent with AGENT.md and scripts. The agent will be stored in the workspace for future use.");

        if (!string.IsNullOrEmpty(language) && language != "auto")
        {
            var langInstruction = language switch
            {
                "zh-TW" => "Always response in Traditional Chinese (繁體中文).",
                "en" => "Always response in English.",
                "ja" => "Always response in Japanese.",
                "kr" => "Always response in Korean.",
                _ => null
            };

            if (langInstruction != null)
            {
                parts.Add(langInstruction);
            }
        }

        return string.Join("\n\n", parts);
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
        // Add user message with images if present
        context.Messages.Add(new ChatMessage(ChatRole.User, context.UserInput, Images: context.Images));

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
                var result = await ExecuteToolCallAsync(toolCall, context.UserId, context.WorkspaceId, ct);
                context.Messages.Add(new ChatMessage(ChatRole.Tool, result, toolCall.Id));
            }
        }

        return "Max iteration reached";
    }

    private async Task<string> ExecuteToolCallAsync(ToolCall toolCall, Guid? userId, Guid? workspaceId, CancellationToken ct)
    {
        if (!_skillMap.TryGetValue(toolCall.Name, out var skill))
        {
            return $"Error: skill '{toolCall.Name}' not found.";
        }

        var skillContext = new ToolContext(toolCall.Arguments)
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            IsSuperAdmin = false
        };
        var result = await skill.ExecuteAsync(skillContext, ct);
        return result.IsSuccess ? result.Output ?? string.Empty : $"Error: {result.Error}";
    }
}