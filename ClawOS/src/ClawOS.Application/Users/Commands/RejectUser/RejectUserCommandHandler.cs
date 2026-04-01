using ErrorOr;

using Mediator;

using ClawOS.Contracts.Users.Commands;
using ClawOS.Domain.Users.Enums;
using ClawOS.Domain.Users.Errors;
using ClawOS.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Application.Users.Commands.RejectUser;

public class RejectUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork uow) : IRequestHandler<RejectUserCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(RejectUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (user.Status != UserStatus.Pending)
        {
            return Error.Validation("User.NotPending", "User is not in pending status.");
        }

        // Delete the rejected user
        await userRepository.DeleteAsync(user, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
