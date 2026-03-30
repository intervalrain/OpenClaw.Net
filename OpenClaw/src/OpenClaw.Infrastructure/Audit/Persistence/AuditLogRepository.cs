using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Audit.Entities;
using OpenClaw.Domain.Audit.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

namespace OpenClaw.Infrastructure.Audit.Persistence;

public class AuditLogRepository(AppDbContext context) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        context.Set<AuditLog>().Add(entry);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(
        Guid? userId = null,
        string? action = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        var query = context.Set<AuditLog>().AsNoTracking().AsQueryable();

        if (userId.HasValue)
            query = query.Where(x => x.UserId == userId.Value);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(x => x.Action == action);

        if (from.HasValue)
            query = query.Where(x => x.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.Timestamp <= to.Value);

        return await query
            .OrderByDescending(x => x.Timestamp)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct = default)
    {
        return await context.Set<AuditLog>()
            .Where(x => x.Timestamp < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
