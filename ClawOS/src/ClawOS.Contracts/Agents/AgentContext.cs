using ClawOS.Contracts.Llm;
using ClawOS.Contracts.Skills;

namespace ClawOS.Contracts.Agents;

public class AgentContext
{
    public required string UserInput { get; init; }
    public IReadOnlyList<ImageContent>? Images { get; init; }
    public required ILlmProvider LlmProvider { get; init; }
    public required IReadOnlyList<IAgentTool> Skills { get; init; }
    public required AgentPipelineOptions Options { get; init; }
    public Guid? UserId { get; init; }
    public List<ChatMessage> Messages { get; } = [];
    public Dictionary<string, object> Items { get; } = [];
}