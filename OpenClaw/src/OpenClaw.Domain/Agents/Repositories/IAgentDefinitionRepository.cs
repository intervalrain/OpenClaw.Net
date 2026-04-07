using OpenClaw.Domain.Agents.Entities;

namespace OpenClaw.Domain.Agents.Repositories;

public interface IAgentDefinitionRepository
{
    Task<AgentDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AgentDefinition?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<AgentDefinition>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(AgentDefinition agent, CancellationToken ct = default);
    Task UpdateAsync(AgentDefinition agent, CancellationToken ct = default);
    Task DeleteAsync(AgentDefinition agent, CancellationToken ct = default);
}
