using ErrorOr;

using Mediator;

using ClawOS.Application.Users.Mapping;
using ClawOS.Contracts.Users.Dtos;
using ClawOS.Contracts.Users.Queries;
using ClawOS.Domain.Users.Repositories;

namespace ClawOS.Application.Users.Queries.ListUsers;

public class ListUsersQueryHandler(
    IUserRepository userRepository) : IRequestHandler<ListUsersQuery, ErrorOr<List<UserDto>>>
{
    public async ValueTask<ErrorOr<List<UserDto>>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);

        return UserMapper.ToDtoList(users);
    }
}
