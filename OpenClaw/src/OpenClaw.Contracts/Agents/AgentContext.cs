using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Contracts.Agents;

public class AgentContext
{
    public required string UserInput { get; init; }
    public required ILlmProvider LlmProvider { get; init; }
    public required IReadOnlyList<IAgentSkill> Skills { get; init; }
    public required AgentPipelineOptions Options { get; init; }
    public List<ChatMessage> Messages { get; } = [];
    public Dictionary<string, object> Items { get; } = [];
}