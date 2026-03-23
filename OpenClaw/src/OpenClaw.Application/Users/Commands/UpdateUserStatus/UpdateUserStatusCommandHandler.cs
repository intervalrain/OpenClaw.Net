using ErrorOr;

using Mediator;

using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Users.Commands.UpdateUserStatus;

public class UpdateUserStatusCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork uow) : IRequestHandler<UpdateUserStatusCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (!Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return Error.Validation("User.InvalidStatus", $"Invalid status: {request.Status}");
        }

        user.UpdateStatus(newStatus);

        await userRepository.UpdateAsync(user, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
