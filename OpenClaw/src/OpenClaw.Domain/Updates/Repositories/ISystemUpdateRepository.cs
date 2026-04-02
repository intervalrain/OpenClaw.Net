using OpenClaw.Domain.Updates.Entities;

namespace OpenClaw.Domain.Updates.Repositories;

public interface ISystemUpdateRepository
{
    Task<SystemUpdate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SystemUpdate?> GetByTagNameAsync(string tagName, CancellationToken ct = default);
    Task<SystemUpdate?> GetLatestAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SystemUpdate>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SystemUpdate>> GetPendingAsync(CancellationToken ct = default);
    Task AddAsync(SystemUpdate entity, CancellationToken ct = default);
    Task UpdateAsync(SystemUpdate entity, CancellationToken ct = default);
}
