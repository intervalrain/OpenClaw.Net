using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;

using OpenClaw.Application.Users.Mapping;
using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Contracts.Users.Queries;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Users.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandler(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider) : IRequestHandler<GetCurrentUserQuery, ErrorOr<UserDto>>
{
    public async ValueTask<ErrorOr<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var currentUser = currentUserProvider.GetCurrentUser();

        var user = await userRepository.GetByIdAsync(currentUser.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        return UserMapper.ToDto(user);
    }
}
