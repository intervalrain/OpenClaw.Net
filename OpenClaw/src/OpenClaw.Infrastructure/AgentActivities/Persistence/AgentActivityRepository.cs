using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.AgentActivities.Entities;
using OpenClaw.Domain.AgentActivities.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.AgentActivities.Persistence;

public class AgentActivityRepository(AppDbContext context)
    : GenericRepository<AgentActivity, Guid, AppDbContext>(context), IAgentActivityRepository
{
    public async Task<IReadOnlyList<AgentActivity>> GetLatestPerUserAsync(CancellationToken ct = default)
    {
        // Get the latest activity per user within the last hour
        var cutoff = DateTime.UtcNow.AddHours(-1);

        return await DbContext.Set<AgentActivity>()
            .Where(a => a.CreatedAt >= cutoff)
            .GroupBy(a => a.UserId)
            .Select(g => g.OrderByDescending(a => a.CreatedAt).First())
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgentActivity>> GetByTimeRangeAsync(
        DateTime from, DateTime to, int limit = 1000, CancellationToken ct = default)
    {
        return await DbContext.Set<AgentActivity>()
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to)
            .OrderBy(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        return await DbContext.Set<AgentActivity>()
            .Where(a => a.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
