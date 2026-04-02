using OpenClaw.Domain.Notifications.Entities;

namespace OpenClaw.Domain.Notifications.Repositories;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByUserAsync(Guid userId, bool unreadOnly = false, int limit = 50, int offset = 0, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Notification entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Notification> entities, CancellationToken ct = default);
    Task UpdateAsync(Notification entity, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
