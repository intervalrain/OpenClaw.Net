using ErrorOr;

using Mediator;

using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;

namespace OpenClaw.Application.Users.Commands.DeleteUserPreference;

public class DeleteUserPreferenceCommandHandler(
    ILogger<DeleteUserPreferenceCommandHandler> logger,
    IUserPreferenceRepository repository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork uow) : IRequestHandler<DeleteUserPreferenceCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteUserPreferenceCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetCurrentUser().Id;
        var preference = await repository.GetByKeyAsync(userId, request.Key, ct);

        if (preference is null)
            return UserPreferenceErrors.NotFound;

        await repository.DeleteAsync(preference, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("User preference deleted: {Key}", request.Key);

        return Result.Success;
    }
}
