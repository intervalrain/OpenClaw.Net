using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Workflows;

/// <summary>
/// Background service that executes scheduled workflows.
/// Checks every minute for workflows that are due to run.
/// </summary>
public class WorkflowSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<WorkflowSchedulerService> logger) : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Workflow scheduler service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndExecuteScheduledWorkflowsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error checking scheduled workflows");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        logger.LogInformation("Workflow scheduler service stopping");
    }

    private async Task CheckAndExecuteScheduledWorkflowsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionRepository>();
        var executor = scope.ServiceProvider.GetRequiredService<IWorkflowExecutor>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var scheduledWorkflows = await repository.GetScheduledWorkflowsAsync(ct);

        foreach (var workflow in scheduledWorkflows)
        {
            try
            {
                var schedule = ParseSchedule(workflow.ScheduleJson);
                if (schedule is null || !schedule.IsEnabled)
                {
                    continue;
                }

                if (IsDue(schedule, now, workflow.LastScheduledAt))
                {
                    logger.LogInformation(
                        "Executing scheduled workflow {WorkflowId}: {WorkflowName}",
                        workflow.Id,
                        workflow.Name);

                    // Mark as scheduled to prevent duplicate executions
                    workflow.MarkScheduledExecution();
                    await uow.SaveChangesAsync(ct);

                    // Start execution
                    await executor.StartAsync(
                        workflow,
                        inputJson: null,
                        userId: workflow.CreatedByUserId,
                        ExecutionTrigger.Scheduled,
                        ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to execute scheduled workflow {WorkflowId}: {WorkflowName}",
                    workflow.Id,
                    workflow.Name);
            }
        }
    }

    private static ScheduleConfig? ParseSchedule(string? scheduleJson)
    {
        if (string.IsNullOrEmpty(scheduleJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScheduleConfig>(
                scheduleJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDue(ScheduleConfig schedule, DateTime utcNow, DateTime? lastScheduledAt)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

        // Check if already executed today (or this week/month depending on frequency)
        if (lastScheduledAt.HasValue)
        {
            var lastLocalTime = TimeZoneInfo.ConvertTimeFromUtc(lastScheduledAt.Value, tz);

            switch (schedule.Frequency)
            {
                case ScheduleFrequency.Daily:
                    if (lastLocalTime.Date == localTime.Date)
                        return false;
                    break;

                case ScheduleFrequency.Weekly:
                    // Same week check
                    var lastWeekStart = lastLocalTime.Date.AddDays(-(int)lastLocalTime.DayOfWeek);
                    var currentWeekStart = localTime.Date.AddDays(-(int)localTime.DayOfWeek);
                    if (lastWeekStart == currentWeekStart)
                        return false;
                    break;

                case ScheduleFrequency.Monthly:
                    if (lastLocalTime.Year == localTime.Year && lastLocalTime.Month == localTime.Month)
                        return false;
                    break;
            }
        }

        // Check time of day (allow 1-minute window)
        var scheduleTime = schedule.TimeOfDay.ToTimeSpan();
        var currentTime = localTime.TimeOfDay;
        var timeDiff = Math.Abs((currentTime - scheduleTime).TotalMinutes);

        if (timeDiff > 1)
        {
            return false;
        }

        // Check day of week/month based on frequency
        return schedule.Frequency switch
        {
            ScheduleFrequency.Daily => true,

            ScheduleFrequency.Weekly =>
                schedule.DaysOfWeek?.Contains(localTime.DayOfWeek) ?? false,

            ScheduleFrequency.Monthly =>
                schedule.DayOfMonth == localTime.Day,

            _ => false
        };
    }
}
