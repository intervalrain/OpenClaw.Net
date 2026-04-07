using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Agents.Entities;
using OpenClaw.Domain.Agents.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

namespace OpenClaw.Infrastructure.Agents.Persistence;

public class AgentDefinitionRepository(AppDbContext db) : IAgentDefinitionRepository
{
    public async Task<AgentDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Set<AgentDefinition>().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AgentDefinition?> GetByNameAsync(string name, CancellationToken ct = default)
        => await db.Set<AgentDefinition>().FirstOrDefaultAsync(a => a.Name == name, ct);

    public async Task<List<AgentDefinition>> GetAllAsync(CancellationToken ct = default)
        => await db.Set<AgentDefinition>().OrderBy(a => a.Name).ToListAsync(ct);

    public async Task AddAsync(AgentDefinition agent, CancellationToken ct = default)
        => await db.Set<AgentDefinition>().AddAsync(agent, ct);

    public Task UpdateAsync(AgentDefinition agent, CancellationToken ct = default)
    {
        db.Set<AgentDefinition>().Update(agent);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AgentDefinition agent, CancellationToken ct = default)
    {
        db.Set<AgentDefinition>().Remove(agent);
        return Task.CompletedTask;
    }
}
