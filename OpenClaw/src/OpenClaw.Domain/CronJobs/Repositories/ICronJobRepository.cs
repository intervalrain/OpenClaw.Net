using OpenClaw.Domain.CronJobs.Entities;

namespace OpenClaw.Domain.CronJobs.Repositories;

public interface ICronJobRepository
{
    Task<CronJob?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CronJob>> GetAllByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CronJob>> GetScheduledJobsAsync(CancellationToken ct = default);
    Task AddAsync(CronJob cronJob, CancellationToken ct = default);
    Task UpdateAsync(CronJob cronJob, CancellationToken ct = default);
    Task DeleteAsync(CronJob cronJob, CancellationToken ct = default);
}
