using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.ToolStore.Entities;
using OpenClaw.Domain.ToolStore.Enums;
using OpenClaw.Domain.ToolStore.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.ToolStore.Persistence;

public class ToolPackageRepository(AppDbContext context)
    : GenericRepository<ToolPackage, Guid, AppDbContext>(context), IToolPackageRepository
{
    public async Task<ToolPackage?> GetByPackageIdAsync(string packageId, CancellationToken ct = default)
    {
        return await DbContext.Set<ToolPackage>()
            .FirstOrDefaultAsync(x => x.PackageId == packageId, ct);
    }

    public new async Task<IReadOnlyList<ToolPackage>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<ToolPackage>()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ToolPackage>> GetInstalledAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<ToolPackage>()
            .Where(x => x.Status == ToolPackageStatus.Installed || x.Status == ToolPackageStatus.UpdateAvailable)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }
}
