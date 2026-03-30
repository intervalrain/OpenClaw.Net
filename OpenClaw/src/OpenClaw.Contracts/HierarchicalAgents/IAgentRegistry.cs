namespace OpenClaw.Contracts.HierarchicalAgents;

/// <summary>
/// Registry for discovering and retrieving agents by name.
/// Auto-discovers IAgent implementations and wraps IAgentTool instances as ToolAgents.
/// </summary>
public interface IAgentRegistry
{
    IAgent? GetAgent(string name);
    IReadOnlyList<IAgent> GetAll();
    void Register(IAgent agent);
}
