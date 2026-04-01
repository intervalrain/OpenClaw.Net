using ErrorOr;
using Mediator;
using ClawOS.Contracts.CronJobs.Commands;
using ClawOS.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace ClawOS.Application.CronJobs.Commands;

public class DeleteToolInstanceCommandHandler(
    IToolInstanceRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteToolInstanceCommand, ErrorOr<bool>>
{
    public async ValueTask<ErrorOr<bool>> Handle(
        DeleteToolInstanceCommand request,
        CancellationToken ct)
    {
        var instance = await repository.GetByIdAsync(request.Id, ct);
        if (instance is null)
        {
            return Error.NotFound($"ToolInstance {request.Id} not found");
        }

        if (instance.CreatedByUserId != request.UserId)
        {
            return Error.Forbidden("You can only delete your own tool instances");
        }

        await repository.DeleteAsync(instance, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return true;
    }
}
