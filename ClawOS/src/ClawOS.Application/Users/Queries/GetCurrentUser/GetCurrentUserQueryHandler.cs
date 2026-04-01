using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;

using ClawOS.Application.Users.Mapping;
using ClawOS.Contracts.Users.Dtos;
using ClawOS.Contracts.Users.Queries;
using ClawOS.Domain.Users.Errors;
using ClawOS.Domain.Users.Repositories;

namespace ClawOS.Application.Users.Queries.GetCurrentUser;

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
