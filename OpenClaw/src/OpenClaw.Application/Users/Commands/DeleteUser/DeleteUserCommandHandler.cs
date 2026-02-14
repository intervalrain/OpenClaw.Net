using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;

using Weda.Core.Application.Security.Models;
using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Users.Commands.DeleteUser;

public class DeleteUserCommandHandler(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider) : IRequestHandler<DeleteUserCommand, ErrorOr<Deleted>>
{
    public async ValueTask<ErrorOr<Deleted>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var currentUser = currentUserProvider.GetCurrentUser();

        // Prevent deleting yourself
        if (user.Id == currentUser.Id)
        {
            return UserErrors.CannotDeleteSelf;
        }

        // Prevent non-SuperAdmin from deleting SuperAdmin
        if (user.Roles.Contains(Role.SuperAdmin) && !currentUser.Roles.Contains(Role.SuperAdmin))
        {
            return UserErrors.CannotDeleteSuperAdmin;
        }

        await userRepository.DeleteAsync(user, cancellationToken);

        return Result.Deleted;
    }
}
