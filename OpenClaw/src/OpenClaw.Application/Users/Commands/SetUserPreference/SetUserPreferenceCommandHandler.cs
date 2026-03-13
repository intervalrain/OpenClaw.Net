using ErrorOr;

using Mediator;

using Microsoft.Extensions.Logging;

using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;

namespace OpenClaw.Application.Users.Commands.SetUserPreference;

public class SetUserPreferenceCommandHandler(
    ILogger<SetUserPreferenceCommandHandler> logger,
    IUserPreferenceRepository repository,
    ICurrentUserProvider currentUserProvider,
    IUnitOfWork uow) : IRequestHandler<SetUserPreferenceCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(SetUserPreferenceCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetCurrentUser().Id;
        var existing = await repository.GetByKeyAsync(userId, request.Key, ct);

        if (existing is null)
        {
            var preference = UserPreference.Create(userId, request.Key, request.Value);
            await repository.AddAsync(preference, ct);
        }
        else
        {
            existing.SetValue(request.Value);
            await repository.UpdateAsync(existing, ct);
        }

        await uow.SaveChangesAsync(ct);

        logger.LogInformation(existing is null 
            ? "User preference created: {Key} = {Value}" 
            : "User preference updated: {Key} = {Value}", request.Key, request.Value);

        return Result.Success;
    }
}
