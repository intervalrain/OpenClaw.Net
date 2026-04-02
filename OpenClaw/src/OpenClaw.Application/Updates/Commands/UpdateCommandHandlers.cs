using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Updates.Commands;
using OpenClaw.Domain.Updates.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Updates.Commands;

public class AcknowledgeUpdateCommandHandler(ISystemUpdateRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<AcknowledgeUpdateCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(AcknowledgeUpdateCommand request, CancellationToken ct)
    {
        var update = await repository.GetByIdAsync(request.UpdateId, ct);
        if (update is null)
            return Error.NotFound("Update not found.");

        update.Acknowledge(request.UserId);
        await repository.UpdateAsync(update, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}

public class DismissUpdateCommandHandler(ISystemUpdateRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<DismissUpdateCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DismissUpdateCommand request, CancellationToken ct)
    {
        var update = await repository.GetByIdAsync(request.UpdateId, ct);
        if (update is null)
            return Error.NotFound("Update not found.");

        update.Dismiss(request.UserId);
        await repository.UpdateAsync(update, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return Result.Success;
    }
}
