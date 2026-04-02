using OpenClaw.Domain.Notifications.Enums;
using Weda.Core.Domain;

namespace OpenClaw.Domain.Notifications.Entities;

/// <summary>
/// In-app notification for users. Covers skill updates, system updates, review outcomes, etc.
/// </summary>
public class Notification : Entity<Guid>, IUserScoped
{
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Message { get; private set; }
    public string? ReferenceId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public Guid GetOwnerUserId() => UserId;

    private Notification() : base(Guid.NewGuid()) { }

    public static Notification Create(
        Guid userId,
        NotificationType type,
        string title,
        string? message = null,
        string? referenceId = null)
    {
        return new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public void MarkAsRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}
