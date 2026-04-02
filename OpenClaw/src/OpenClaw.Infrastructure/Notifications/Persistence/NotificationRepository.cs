using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Notifications.Entities;
using OpenClaw.Domain.Notifications.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Notifications.Persistence;

public class NotificationRepository(AppDbContext dbContext)
    : GenericRepository<Notification, Guid, AppDbContext>(dbContext), INotificationRepository
{
    public async Task<List<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = DbContext.Set<Notification>()
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<Notification>()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }
}
