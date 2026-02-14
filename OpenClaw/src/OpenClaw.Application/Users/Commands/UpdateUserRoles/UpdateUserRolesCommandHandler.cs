using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;

using OpenClaw.Application.Users.Mapping;
using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Users.Commands.UpdateUserRoles;

public class UpdateUserRolesCommandHandler(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider) : IRequestHandler<UpdateUserRolesCommand, ErrorOr<UserDto>>
{
    public async ValueTask<ErrorOr<UserDto>> Handle(UpdateUserRolesCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        var currentUser = currentUserProvider.GetCurrentUser();

        var updateResult = user.UpdateRoles(request.Roles, currentUser.Id, currentUser.Roles);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        if (request.Permissions is not null)
        {
            var permissionsResult = user.UpdatePermissions(request.Permissions, currentUser.Roles);
            if (permissionsResult.IsError)
            {
                return permissionsResult.Errors;
            }
        }

        await userRepository.UpdateAsync(user, cancellationToken);

        return UserMapper.ToDto(user);
    }
}
