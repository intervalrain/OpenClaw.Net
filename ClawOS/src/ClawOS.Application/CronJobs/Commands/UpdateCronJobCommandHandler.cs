using ErrorOr;
using Mediator;
using ClawOS.Contracts.CronJobs.Commands;
using ClawOS.Contracts.CronJobs.Responses;
using ClawOS.Domain.CronJobs;
using ClawOS.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace ClawOS.Application.CronJobs.Commands;

public class UpdateCronJobCommandHandler(
    ICronJobRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateCronJobCommand, ErrorOr<CronJobResponse>>
{
    public async ValueTask<ErrorOr<CronJobResponse>> Handle(
        UpdateCronJobCommand request,
        CancellationToken ct)
    {
        var job = await repository.GetByIdAsync(request.Id, ct);
        if (job is null)
        {
            return Error.NotFound($"CronJob {request.Id} not found");
        }

        if (job.CreatedByUserId != request.UserId)
        {
            return Error.Forbidden("You can only update your own cron jobs");
        }

        WakeMode? wakeMode = null;
        if (request.WakeMode is not null)
        {
            if (!Enum.TryParse<WakeMode>(request.WakeMode, ignoreCase: true, out var parsed))
            {
                return Error.Validation($"Invalid WakeMode '{request.WakeMode}'");
            }
            wakeMode = parsed;
        }

        job.Update(
            request.Name,
            request.ScheduleJson,
            request.Content,
            wakeMode,
            request.SessionId,
            request.ContextJson,
            request.IsActive);

        await repository.UpdateAsync(job, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return CreateCronJobCommandHandler.ToResponse(job);
    }
}
