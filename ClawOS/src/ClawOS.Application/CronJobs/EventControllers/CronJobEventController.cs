using Asp.Versioning;
using Microsoft.Extensions.Logging;
using ClawOS.Contracts.CronJobs.Events;
using ClawOS.Domain.CronJobs;
using ClawOS.Domain.CronJobs.Repositories;
using Weda.Core.Infrastructure.Messaging.Nats;
using Weda.Core.Infrastructure.Messaging.Nats.Attributes;
using Weda.Core.Infrastructure.Messaging.Nats.Exceptions;

namespace ClawOS.Application.CronJobs.EventControllers;

/// <summary>
/// JetStream consumer for cron job execution.
/// Multiple instances compete for jobs via durable consumer group.
/// Transient failures NAK + retry; permanent failures go to DLQ.
/// </summary>
[ApiVersion("1")]
public class CronJobEventController(
    ICronJobRepository jobRepo,
    ICronJobExecutor executor) : EventController
{
    [Subject("eco1j.weda.{jobId}.clawos.cronjob.execute",
        Stream = "cronjob_execute_stream",
        Consumer = "cronjob_execute_worker")]
    public async Task OnExecute(CronJobExecuteEvent @event)
    {
        Logger.LogInformation("Worker received cron job: JobId={JobId}, Trigger={Trigger}",
            @event.JobId, @event.Trigger);

        var job = await jobRepo.GetByIdAsync(@event.JobId);
        if (job is null)
        {
            Logger.LogWarning("Cron job {JobId} not found, skipping", @event.JobId);
            return;
        }

        var trigger = Enum.TryParse<ExecutionTrigger>(@event.Trigger, out var t)
            ? t
            : ExecutionTrigger.Scheduled;

        try
        {
            await executor.ExecuteAsync(job, @event.UserId, trigger);
            Logger.LogInformation("Cron job {JobId} executed successfully", @event.JobId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Cron job {JobId} execution failed", @event.JobId);
            throw new TransientException($"Cron job {@event.JobId} failed: {ex.Message}", ex);
        }
    }
}
