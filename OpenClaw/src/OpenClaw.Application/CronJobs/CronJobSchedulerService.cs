using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.CronJobs;
using OpenClaw.Domain.CronJobs;
using OpenClaw.Domain.CronJobs.Entities;
using OpenClaw.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.CronJobs;

public class CronJobSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<CronJobSchedulerService> logger) : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Cron job scheduler service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndExecuteScheduledJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking scheduled cron jobs");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndExecuteScheduledJobsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<ICronJobRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<ICronJobExecutor>();
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

                if (IsDue(schedule, job.LastScheduledAt))
                {
                    logger.LogInformation("Executing scheduled cron job: {Name} ({Id})", job.Name, job.Id);

                    job.MarkScheduledExecution();
                    await uow.SaveChangesAsync(ct);

                    await executor.ExecuteAsync(job, job.CreatedByUserId, ExecutionTrigger.Scheduled, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute scheduled cron job {Id}", job.Id);
            }
        }
    }

    private static bool IsDue(ScheduleConfig schedule, DateTime? lastScheduledAt)
    {
        var now = DateTime.UtcNow;

        // Parse timezone
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

        // Check if we've already run today/this period
        if (lastScheduledAt.HasValue)
        {
            var localLastRun = TimeZoneInfo.ConvertTimeFromUtc(lastScheduledAt.Value, tz);
            if (localLastRun.Date == localNow.Date && localLastRun.TimeOfDay >= scheduledTime)
            {
                return false;
            }
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
