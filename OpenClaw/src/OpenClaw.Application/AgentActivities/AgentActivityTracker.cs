using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenClaw.Domain.AgentActivities;
using OpenClaw.Domain.AgentActivities.Entities;
using OpenClaw.Domain.AgentActivities.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.AgentActivities;

/// <summary>
/// Tracks agent activity by persisting to DB and broadcasting to real-time subscribers.
/// Registered as Singleton so it can be injected into other singletons (e.g., CronJobExecutor).
/// Uses IServiceScopeFactory to resolve scoped DB dependencies.
/// </summary>
public class AgentActivityTracker(
    IServiceScopeFactory scopeFactory,
    IAgentActivityBroadcast broadcast,
    ILogger<AgentActivityTracker> logger) : IAgentActivityTracker
{
    public async Task TrackAsync(
        Guid userId,
        string userName,
        ActivityType type,
        ActivityStatus status,
        string? sourceId = null,
        string? sourceName = null,
        string? detail = null,
        CancellationToken ct = default)
    {
        var activity = AgentActivity.Create(userId, userName, type, status, sourceId, sourceName, detail);

        // Broadcast to SSE subscribers (fire-and-forget style, but awaited)
        var evt = new AgentActivityEvent(
            activity.Id, userId, userName, type, status,
            sourceId, sourceName, detail, activity.CreatedAt);

        await broadcast.PublishAsync(evt, ct);

        // Persist to DB asynchronously — don't block the caller on DB writes
        _ = PersistAsync(activity);
    }

    private async Task PersistAsync(AgentActivity activity)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAgentActivityRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await repo.AddAsync(activity);
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist agent activity {ActivityId}", activity.Id);
        }
    }
}
