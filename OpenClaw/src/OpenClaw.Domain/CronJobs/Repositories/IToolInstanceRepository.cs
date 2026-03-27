using OpenClaw.Domain.CronJobs.Entities;

namespace OpenClaw.Domain.CronJobs.Repositories;

public interface IToolInstanceRepository
{
    Task<ToolInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ToolInstance>> GetAllByUserAsync(Guid userId, CancellationToken ct = default);
    Task<ToolInstance?> GetByNameAsync(string name, Guid userId, CancellationToken ct = default);
    Task AddAsync(ToolInstance toolInstance, CancellationToken ct = default);
    Task UpdateAsync(ToolInstance toolInstance, CancellationToken ct = default);
    Task DeleteAsync(ToolInstance toolInstance, CancellationToken ct = default);
}
