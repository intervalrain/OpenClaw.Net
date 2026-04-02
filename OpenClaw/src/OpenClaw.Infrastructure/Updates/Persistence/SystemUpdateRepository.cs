using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Updates.Entities;
using OpenClaw.Domain.Updates.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Updates.Persistence;

public class SystemUpdateRepository(AppDbContext context)
    : GenericRepository<SystemUpdate, Guid, AppDbContext>(context), ISystemUpdateRepository
{
    public async Task<SystemUpdate?> GetByTagNameAsync(string tagName, CancellationToken ct = default)
    {
        return await DbContext.Set<SystemUpdate>()
            .FirstOrDefaultAsync(x => x.TagName == tagName, ct);
    }

    public async Task<SystemUpdate?> GetLatestAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<SystemUpdate>()
            .OrderByDescending(x => x.PublishedAt)
            .FirstOrDefaultAsync(ct);
    }

    public new async Task<IReadOnlyList<SystemUpdate>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<SystemUpdate>()
            .OrderByDescending(x => x.PublishedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SystemUpdate>> GetPendingAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<SystemUpdate>()
            .Where(x => !x.IsAcknowledged && !x.IsDismissed)
            .OrderByDescending(x => x.PublishedAt)
            .ToListAsync(ct);
    }
}
