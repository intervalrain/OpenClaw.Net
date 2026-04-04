using System.Collections.Concurrent;
using OpenClaw.Contracts.HierarchicalAgents;

namespace OpenClaw.Application.HierarchicalAgents;

public class AgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, IAgent> _agents = new(StringComparer.OrdinalIgnoreCase);

    public AgentRegistry(IEnumerable<IAgent> agents)
    {
        foreach (var agent in agents)
        {
            _agents[agent.Name] = agent;
        }
    }

    public IAgent? GetAgent(string name) => _agents.GetValueOrDefault(name);

    public IReadOnlyList<IAgent> GetAll() => _agents.Values.ToList();

    public void Register(IAgent agent) => _agents[agent.Name] = agent;
}
