namespace OpenClaw.Contracts.Skills;

/// <summary>
/// Store for loading markdown-defined agent definitions (AGENT.md).
/// </summary>
public interface IAgentStore
{
    IReadOnlyList<AgentDefinition> GetAllAgents();
    AgentDefinition? GetAgent(string name);
    Task ReloadAsync(CancellationToken ct = default);
}
