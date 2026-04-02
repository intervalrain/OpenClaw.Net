using OpenClaw.Domain.ToolStore.Entities;

namespace OpenClaw.Domain.ToolStore.Repositories;

public interface IToolPackageRepository
{
    Task<ToolPackage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ToolPackage?> GetByPackageIdAsync(string packageId, CancellationToken ct = default);
    Task<IReadOnlyList<ToolPackage>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ToolPackage>> GetInstalledAsync(CancellationToken ct = default);
    Task AddAsync(ToolPackage entity, CancellationToken ct = default);
    Task UpdateAsync(ToolPackage entity, CancellationToken ct = default);
    Task DeleteAsync(ToolPackage entity, CancellationToken ct = default);
}
