using Mediator;
using Microsoft.Extensions.Logging;
using OpenClaw.Domain.Notifications.Entities;
using OpenClaw.Domain.Notifications.Enums;
using OpenClaw.Domain.Notifications.Repositories;
using OpenClaw.Domain.SkillStore.Events;
using OpenClaw.Domain.SkillStore.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.SkillStore.EventHandlers;

/// <summary>
/// When a skill version is updated (and approved), notify all followers and users
/// who have installed the skill.
/// </summary>
public class SkillVersionUpdatedEventHandler(
    ISkillFollowRepository followRepository,
    ISkillInstallationRepository installationRepository,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork,
    ILogger<SkillVersionUpdatedEventHandler> logger)
    : INotificationHandler<SkillVersionUpdatedEvent>
{
    public async ValueTask Handle(SkillVersionUpdatedEvent notification, CancellationToken ct)
    {
        logger.LogInformation("Skill {SkillName} updated from {OldVersion} to {NewVersion}",
            notification.SkillName, notification.OldVersion, notification.NewVersion);

        // Mark all installations as having an update
        var installations = await installationRepository.GetBySkillAsync(notification.SkillListingId, ct);
        foreach (var installation in installations)
        {
            installation.MarkUpdateAvailable();
            await installationRepository.UpdateAsync(installation, ct);
        }

        // Collect unique user IDs from followers and installations
        var followers = await followRepository.GetBySkillAsync(notification.SkillListingId, ct);
        var userIds = followers.Select(f => f.UserId)
            .Union(installations.Select(i => i.UserId))
            .Distinct()
            .ToList();

        // Create notifications for all affected users
        var notifications = userIds.Select(userId => Notification.Create(
            userId,
            NotificationType.SkillUpdateAvailable,
            $"Skill '{notification.SkillName}' updated to v{notification.NewVersion}",
            $"A new version ({notification.NewVersion}) is available for skill '{notification.SkillName}'. Previous version: {notification.OldVersion}.",
            notification.SkillListingId.ToString()));

        await notificationRepository.AddRangeAsync(notifications, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public class SkillApprovedEventHandler(
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork,
    ILogger<SkillApprovedEventHandler> logger)
    : INotificationHandler<SkillApprovedEvent>
{
    public async ValueTask Handle(SkillApprovedEvent notification, CancellationToken ct)
    {
        logger.LogInformation("Skill {SkillName} approved by {ReviewerId}",
            notification.SkillName, notification.ReviewedByUserId);
        // Note: We don't know the author's user ID from the event alone.
        // The domain event is published after SaveChanges, so we can't query the listing here
        // without another scope. The notification to the author is handled in the command handler flow.
    }
}

public class SkillRejectedEventHandler(
    ILogger<SkillRejectedEventHandler> logger)
    : INotificationHandler<SkillRejectedEvent>
{
    public async ValueTask Handle(SkillRejectedEvent notification, CancellationToken ct)
    {
        logger.LogInformation("Skill {SkillName} rejected by {ReviewerId}: {Reason}",
            notification.SkillName, notification.ReviewedByUserId, notification.Reason);
        await ValueTask.CompletedTask;
    }
}
