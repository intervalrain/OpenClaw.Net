using OpenClaw.Domain.Notifications.Entities;
using Weda.Core.Domain;

namespace OpenClaw.Domain.Notifications.Repositories;

public interface INotificationRepository : IRepository<Notification, Guid>
{
    Task<List<Notification>> GetByUserIdAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}
