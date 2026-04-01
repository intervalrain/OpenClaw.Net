using Microsoft.EntityFrameworkCore;
using ClawOS.Domain.CronJobs;
using ClawOS.Domain.CronJobs.Entities;
using ClawOS.Domain.CronJobs.Repositories;
using ClawOS.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace ClawOS.Infrastructure.CronJobs.Persistence;

public class CronJobRepository(AppDbContext context)
    : GenericRepository<CronJob, Guid, AppDbContext>(context), ICronJobRepository
{
    public async Task<IReadOnlyList<CronJob>> GetAllByUserAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<CronJob>()
            .Where(x => x.CreatedByUserId == userId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CronJob>> GetScheduledJobsAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<CronJob>()
            .Where(x => x.IsActive
                && x.WakeMode != WakeMode.Manual
                && x.ScheduleJson != null)
            .ToListAsync(ct);
    }
}
