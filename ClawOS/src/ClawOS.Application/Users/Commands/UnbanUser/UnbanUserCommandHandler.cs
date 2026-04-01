using ErrorOr;

using Mediator;

using ClawOS.Contracts.Users.Commands;
using ClawOS.Domain.Users.Enums;
using ClawOS.Domain.Users.Errors;
using ClawOS.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Application.Users.Commands.UnbanUser;

public class UnbanUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork uow) : IRequestHandler<UnbanUserCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.Status != UserStatus.Banned)
        {
            return Error.Validation("User.NotBanned", "User is not banned.");
        }

        user.Unban();

        await userRepository.UpdateAsync(user, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
