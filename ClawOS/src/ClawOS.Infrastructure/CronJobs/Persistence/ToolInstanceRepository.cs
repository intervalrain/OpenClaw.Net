using Microsoft.EntityFrameworkCore;
using ClawOS.Domain.CronJobs.Entities;
using ClawOS.Domain.CronJobs.Repositories;
using ClawOS.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace ClawOS.Infrastructure.CronJobs.Persistence;

public class ToolInstanceRepository(AppDbContext context)
    : GenericRepository<ToolInstance, Guid, AppDbContext>(context), IToolInstanceRepository
{
    public async Task<IReadOnlyList<ToolInstance>> GetAllByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<ToolInstance>()
            .Where(x => x.CreatedByUserId == userId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<ToolInstance?> GetByNameAsync(
        string name, Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<ToolInstance>()
            .FirstOrDefaultAsync(x =>
                x.Name.ToLower() == name.ToLower()
                && x.CreatedByUserId == userId, ct);
    }
}
