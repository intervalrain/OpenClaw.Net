using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Notifications.Entities;
using OpenClaw.Domain.Notifications.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Notifications.Persistence;

public class NotificationRepository(AppDbContext context)
    : GenericRepository<Notification, Guid, AppDbContext>(context), INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetByUserAsync(
        Guid userId, bool unreadOnly, int limit, int offset, CancellationToken ct)
    {
        var query = DbContext.Set<Notification>()
            .Where(x => x.UserId == userId);

        if (unreadOnly)
            query = query.Where(x => !x.IsRead);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<Notification>()
            .CountAsync(x => x.UserId == userId && !x.IsRead, ct);
    }

    public async Task AddRangeAsync(IEnumerable<Notification> entities, CancellationToken ct = default)
    {
        await DbContext.Set<Notification>().AddRangeAsync(entities, ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await DbContext.Set<Notification>()
            .Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsRead, true)
                .SetProperty(x => x.ReadAt, DateTime.UtcNow), ct);
    }
}
