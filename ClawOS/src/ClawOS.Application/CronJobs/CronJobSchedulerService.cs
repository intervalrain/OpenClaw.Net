using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ClawOS.Contracts.CronJobs;
using ClawOS.Contracts.CronJobs.Events;
using ClawOS.Domain.CronJobs;
using ClawOS.Domain.CronJobs.Entities;
using ClawOS.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Interfaces.Messaging;
using Weda.Core.Infrastructure.Messaging.Nats.Locking;

namespace ClawOS.Application.CronJobs;

/// <summary>
/// Distributed cron job scheduler.
/// Uses NATS KV lock for leader election — only one instance schedules.
/// Publishes due jobs to JetStream for competing consumer execution.
/// Falls back to in-process execution if NATS is unavailable.
/// </summary>
public class CronJobSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<CronJobSchedulerService> logger) : BackgroundService
{
    private const string LockName = "cronjob_scheduler_leader";
    private const string SubjectPrefix = "eco1j.weda.{0}.clawos.cronjob.execute";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Cron job scheduler service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScheduleLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in cron job scheduler loop");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task ScheduleLoopAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();

        // Try to acquire leader lock via NATS KV
        var distributedLock = scope.ServiceProvider.GetService<NatsKvDistributedLock>();
        if (distributedLock is not null)
        {
            var isLeader = await distributedLock.TryAcquireAsync(LockName, LockTtl, ct);
            if (!isLeader)
            {
                logger.LogDebug("Not the scheduler leader, skipping this cycle");
                return;
            }
        }

        // We are the leader (or NATS unavailable — single instance fallback)
        var jobRepo = scope.ServiceProvider.GetRequiredService<ICronJobRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var jobs = await jobRepo.GetScheduledJobsAsync(ct);

        foreach (var job in jobs)
        {
            if (string.IsNullOrEmpty(job.ScheduleJson)) continue;

            try
            {
                var schedule = JsonSerializer.Deserialize<ScheduleConfig>(
                    job.ScheduleJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (schedule is null || !schedule.IsEnabled) continue;

                if (!IsDue(schedule, job.LastScheduledAt)) continue;

                logger.LogInformation("Dispatching scheduled cron job: {Name} ({Id})", job.Name, job.Id);

                job.MarkScheduledExecution();
                await uow.SaveChangesAsync(ct);

                // Try to publish to NATS JetStream for distributed execution
                var published = await TryPublishToNatsAsync(scope, job, ct);

                if (!published)
                {
                    // Fallback: execute in-process
                    logger.LogDebug("NATS unavailable, executing cron job in-process: {Id}", job.Id);
                    var executor = scope.ServiceProvider.GetRequiredService<ICronJobExecutor>();
                    await executor.ExecuteAsync(job, job.CreatedByUserId, ExecutionTrigger.Scheduled, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to schedule cron job {Id}", job.Id);
            }
        }
    }

    private async Task<bool> TryPublishToNatsAsync(IServiceScope scope, CronJob job, CancellationToken ct)
    {
        try
        {
            var clientFactory = scope.ServiceProvider.GetService<IJetStreamClientFactory>();
            if (clientFactory is null) return false;

            var client = clientFactory.Create("bus");
            var evt = new CronJobExecuteEvent(
                JobId: job.Id,
                UserId: job.CreatedByUserId,
                Trigger: ExecutionTrigger.Scheduled.ToString()
            );

            var subject = string.Format(SubjectPrefix, job.Id);
            await client.JsPublishAsync(subject, evt, ct);
            logger.LogInformation("Published cron job {Id} to NATS subject {Subject}", job.Id, subject);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish cron job {Id} to NATS, will fall back to in-process", job.Id);
            return false;
        }
    }

    private static bool IsDue(ScheduleConfig schedule, DateTime? lastScheduledAt)
    {
        var now = DateTime.UtcNow;

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone ?? "UTC");
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
        var scheduledTime = schedule.TimeOfDay.ToTimeSpan();

        if (lastScheduledAt.HasValue)
        {
            var localLastRun = TimeZoneInfo.ConvertTimeFromUtc(lastScheduledAt.Value, tz);
            if (localLastRun.Date == localNow.Date && localLastRun.TimeOfDay >= scheduledTime)
                return false;
        }

        if (localNow.TimeOfDay < scheduledTime) return false;

        return schedule.Frequency switch
        {
            ScheduleFrequency.Daily => true,
            ScheduleFrequency.Weekly => schedule.DaysOfWeek?.Contains(localNow.DayOfWeek) ?? true,
            ScheduleFrequency.Monthly => localNow.Day == (schedule.DayOfMonth ?? 1),
            _ => false
        };
    }
}
