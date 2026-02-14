using ErrorOr;

using Mediator;

using OpenClaw.Application.Users.Mapping;
using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Contracts.Users.Queries;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Users.Queries.ListUsers;

public class ListUsersQueryHandler(
    IUserRepository userRepository) : IRequestHandler<ListUsersQuery, ErrorOr<List<UserDto>>>
{
    public async ValueTask<ErrorOr<List<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);

        return UserMapper.ToDtoList(users);
    }
}
