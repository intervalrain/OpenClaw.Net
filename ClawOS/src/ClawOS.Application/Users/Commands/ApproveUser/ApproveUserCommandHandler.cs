using ErrorOr;

using Mediator;

using ClawOS.Contracts.Users.Commands;
using ClawOS.Domain.Users.Enums;
using ClawOS.Domain.Users.Errors;
using ClawOS.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;

namespace ClawOS.Application.Users.Commands.ApproveUser;

public class ApproveUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork uow) : IRequestHandler<ApproveUserCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(ApproveUserCommand request, CancellationToken cancellationToken)
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

        user.UpdateStatus(UserStatus.Active);

        await userRepository.UpdateAsync(user, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
