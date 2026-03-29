using ErrorOr;
using Mediator;
using OpenClaw.Contracts.CronJobs.Commands;
using OpenClaw.Contracts.CronJobs.Responses;
using OpenClaw.Domain.CronJobs.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.CronJobs.Commands;

public class UpdateToolInstanceCommandHandler(
    IToolInstanceRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateToolInstanceCommand, ErrorOr<ToolInstanceResponse>>
{
    public async ValueTask<ErrorOr<ToolInstanceResponse>> Handle(
        UpdateToolInstanceCommand request,
        CancellationToken ct)
    {
        var instance = await repository.GetByIdAsync(request.Id, ct);
        if (instance is null)
        {
            return Error.NotFound($"ToolInstance {request.Id} not found");
        }

        if (instance.CreatedByUserId != request.UserId)
        {
            return Error.Forbidden("You can only update your own tool instances");
        }

        instance.Update(
            request.Name,
            request.ToolName,
            request.ArgsJson,
            request.Description);

        await repository.UpdateAsync(instance, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return CreateToolInstanceCommandHandler.ToResponse(instance);
    }
}
