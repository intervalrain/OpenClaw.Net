using ErrorOr;
using Mediator;
using ClawOS.Contracts.CronJobs.Commands;
using ClawOS.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace ClawOS.Application.CronJobs.Commands;

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
