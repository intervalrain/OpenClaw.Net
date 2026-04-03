using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.CronJobs.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.CronJobs.Persistence;

public class CronJobExecutionRepository(AppDbContext context)
    : GenericRepository<CronJobExecution, Guid, AppDbContext>(context), ICronJobExecutionRepository
{
    public async Task<IReadOnlyList<CronJobExecution>> GetByCronJobIdAsync(
        Guid cronJobId, int limit = 20, int offset = 0, CancellationToken ct = default)
    {
        return await DbContext.Set<CronJobExecution>()
            .Where(x => x.CronJobId == cronJobId)
            .OrderByDescending(x => x.StartedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CronJobExecution>> GetByUserAsync(
        Guid userId, int limit = 20, int offset = 0, CancellationToken ct = default)
    {
        return await DbContext.Set<CronJobExecution>()
            .Where(x => x.CronJob!.CreatedByUserId == userId)
            .OrderByDescending(x => x.StartedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task DeleteByCronJobIdAsync(Guid cronJobId, CancellationToken ct = default)
    {
        await DbContext.Set<CronJobExecution>()
            .Where(x => x.CronJobId == cronJobId)
            .ExecuteDeleteAsync(ct);
    }
}
