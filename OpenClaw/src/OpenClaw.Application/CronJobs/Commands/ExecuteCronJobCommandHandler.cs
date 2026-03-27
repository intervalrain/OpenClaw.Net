using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Commands;
using OpenClaw.Domain.CronJobs;
using OpenClaw.Domain.CronJobs.Repositories;

namespace OpenClaw.Application.CronJobs.Commands;

public class ExecuteCronJobCommandHandler(
    ICronJobRepository repository,
    ICronJobExecutor executor) : IRequestHandler<ExecuteCronJobCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(
        ExecuteCronJobCommand request,
        CancellationToken ct)
    {
        var job = await repository.GetByIdAsync(request.Id, ct);
        if (job is null)
        {
            return Error.NotFound($"CronJob {request.Id} not found");
        }

        if (!job.IsActive)
        {
            return Error.Validation("Cannot execute inactive cron job");
        }

        var executionId = await executor.ExecuteAsync(
            job,
            request.UserId,
            ExecutionTrigger.Manual,
            ct);

        return executionId;
    }
}
