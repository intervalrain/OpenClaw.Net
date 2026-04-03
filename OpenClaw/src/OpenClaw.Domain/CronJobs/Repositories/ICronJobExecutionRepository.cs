using OpenClaw.Domain.CronJobs.Entities;

namespace OpenClaw.Domain.CronJobs.Repositories;

public interface ICronJobExecutionRepository
{
    Task<CronJobExecution?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CronJobExecution>> GetByCronJobIdAsync(Guid cronJobId, int limit = 20, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<CronJobExecution>> GetByUserAsync(Guid userId, int limit = 20, int offset = 0, CancellationToken ct = default);
    Task AddAsync(CronJobExecution execution, CancellationToken ct = default);
    Task DeleteByCronJobIdAsync(Guid cronJobId, CancellationToken ct = default);
}
