namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// Registry for discovering and retrieving agents by name.
/// Supports built-in agents (e.g. Pioneer) and workspace-scoped agents loaded from AGENT.md files.
/// </summary>
public interface IAgentRegistry
{
    /// <summary>Gets a built-in agent by name.</summary>
    IAgent? GetAgent(string name);

    /// <summary>Gets an agent by name, including workspace-scoped agents.</summary>
    IAgent? GetAgent(string name, Guid workspaceId);

    /// <summary>Gets all built-in agents.</summary>
    IReadOnlyList<IAgent> GetAll();

    /// <summary>Gets all agents including workspace-scoped agents.</summary>
    IReadOnlyList<IAgent> GetAll(Guid workspaceId);

    /// <summary>Registers a built-in agent.</summary>
    void Register(IAgent agent);
}
