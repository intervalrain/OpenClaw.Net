namespace OpenClaw.Contracts.Skills;

/// <summary>
/// Store for loading markdown-defined agent definitions (AGENT.md).
/// Workspace-scoped: agents are loaded from workspace-specific directories.
/// </summary>
public interface IAgentStore
{
    IReadOnlyList<AgentDefinition> GetAllAgents(Guid workspaceId);
    AgentDefinition? GetAgent(string name, Guid workspaceId);
    Task ReloadAsync(Guid workspaceId, CancellationToken ct = default);
}
