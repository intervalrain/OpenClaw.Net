using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Agents;

public class AgentPipeline(
    ILlmProvider llmProvider,
    IEnumerable<IAgentSkill> skills,
    AgentPipelineOptions options) : IAgentPipeline
{
    private readonly List<ChatMessage> _messages = [];
    private readonly Dictionary<string, IAgentSkill> _skillMap = skills.ToDictionary(s => s.Name);

    public async Task<string> ExecuteAsync(string userInput, CancellationToken ct = default)
    {
        if (_messages.Count == 0 && options.SystemPrompt is not null)
        {
            _messages.Add(new ChatMessage(ChatRole.System, options.SystemPrompt));
        }

        _messages.Add(new ChatMessage(ChatRole.User, userInput));

        var toolDefinitions = _skillMap.Values
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        for (int i = 0; i < options.MaxIterations; i++)
        {
            var response = await llmProvider.ChatAsync(_messages, toolDefinitions, ct);
            
            if (!response.HasToolCalls)
            {
                _messages.Add(new ChatMessage(ChatRole.Assistant, response.Content ?? ""));
                return response.Content ?? "";
            }

            foreach (var toolCall in response.ToolCalls!)
            {
                var result = await ExecuteToolCallAsync(toolCall, ct);
                _messages.Add(new ChatMessage(ChatRole.Tool, result, toolCall.Id));
            }
        }

        return "Max iteration reached";
    }

    private async Task<string> ExecuteToolCallAsync(ToolCall toolCall, CancellationToken ct)
    {
        if (!_skillMap.TryGetValue(toolCall.Name, out var skill))
        {
            return $"Error: Skill '{toolCall.Name}' not found.";
        }

        var context = new SkillContext
        {
            Arguments = toolCall.Arguments,
        };

        var result = await skill.ExecuteAsync(context, ct);

        return result.IsSuccess ? result.Output ?? "" : $"Error: {result.Error}";
    }

}