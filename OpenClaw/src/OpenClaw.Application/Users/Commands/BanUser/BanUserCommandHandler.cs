using ErrorOr;

using Mediator;

using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Users.Commands.BanUser;

public class BanUserCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork uow) : IRequestHandler<BanUserCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var result = user.Ban(request.Reason, user.Roles);
        if (result.IsError)
        {
            return result.Errors;
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
