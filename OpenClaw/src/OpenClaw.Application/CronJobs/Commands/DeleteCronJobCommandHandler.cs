using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Commands;
using OpenClaw.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.CronJobs.Commands;

public class DeleteCronJobCommandHandler(
    ICronJobRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteCronJobCommand, ErrorOr<bool>>
{
    public async ValueTask<ErrorOr<bool>> Handle(
        DeleteCronJobCommand request,
        CancellationToken ct)
    {
        var job = await repository.GetByIdAsync(request.Id, ct);
        if (job is null)
        {
            return Error.NotFound($"CronJob {request.Id} not found");
        }

        if (job.CreatedByUserId != request.UserId)
        {
            return Error.Forbidden("You can only delete your own cron jobs");
        }

        await repository.DeleteAsync(job, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return true;
    }
}
