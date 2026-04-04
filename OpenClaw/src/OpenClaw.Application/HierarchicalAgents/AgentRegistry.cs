using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

public class AgentRegistry : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, IAgent> _builtInAgents = new(StringComparer.OrdinalIgnoreCase);
    private readonly IServiceScopeFactory? _scopeFactory;

    public AgentRegistry(IEnumerable<IAgent> builtInAgents, IServiceScopeFactory? scopeFactory = null)
    {
        _scopeFactory = scopeFactory;

        foreach (var agent in builtInAgents)
        {
            _builtInAgents[agent.Name] = agent;
        }
    }

    /// <summary>Gets a built-in agent by name.</summary>
    public IAgent? GetAgent(string name) => _builtInAgents.GetValueOrDefault(name);

    /// <summary>Gets an agent by name, checking workspace agents first, then built-in.</summary>
    public IAgent? GetAgent(string name, Guid workspaceId)
    {
        // Check workspace agents first (workspace definitions can shadow built-in names)
        var workspaceAgents = LoadWorkspaceAgents(workspaceId);
        var wsAgent = workspaceAgents.FirstOrDefault(a =>
            a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (wsAgent is not null) return wsAgent;

        return GetAgent(name);
    }

    /// <summary>Gets all built-in agents.</summary>
    public IReadOnlyList<IAgent> GetAll() => _builtInAgents.Values.ToList();

    /// <summary>Gets all agents: built-in + workspace-scoped.</summary>
    public IReadOnlyList<IAgent> GetAll(Guid workspaceId)
    {
        var all = new List<IAgent>(_builtInAgents.Values);
        all.AddRange(LoadWorkspaceAgents(workspaceId));
        return all;
    }

    /// <summary>Registers a built-in agent.</summary>
    public void Register(IAgent agent) => _builtInAgents[agent.Name] = agent;

    private IReadOnlyList<IAgent> LoadWorkspaceAgents(Guid workspaceId)
    {
        if (_scopeFactory is null) return [];

        using var scope = _scopeFactory.CreateScope();
        var agentStore = scope.ServiceProvider.GetService<IAgentStore>();
        if (agentStore is null) return [];

        // Ensure agents are loaded for this workspace
        agentStore.ReloadAsync(workspaceId).GetAwaiter().GetResult();

        return agentStore.GetAllAgents(workspaceId)
            .Select(d => (IAgent)new FileDefinedAgent(d))
            .ToList();
    }
}
