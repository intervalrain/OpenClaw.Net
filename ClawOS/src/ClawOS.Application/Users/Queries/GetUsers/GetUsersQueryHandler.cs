using ErrorOr;

using Mediator;

using ClawOS.Application.Users.Mapping;
using ClawOS.Contracts.Users.Dtos;
using ClawOS.Contracts.Users.Queries;
using ClawOS.Domain.Users.Enums;
using ClawOS.Domain.Users.Repositories;

namespace ClawOS.Application.Users.Queries.GetUsers;

public class GetUsersQueryHandler(
    IUserRepository userRepository) : IRequestHandler<GetUsersQuery, ErrorOr<List<UserDto>>>
{
    public async ValueTask<ErrorOr<List<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);

        // Filter by status if specified
        if (!string.IsNullOrWhiteSpace(request.StatusFilter) &&
            Enum.TryParse<UserStatus>(request.StatusFilter, ignoreCase: true, out var status))
        {
            users = users.Where(u => u.Status == status).ToList();
        }

        return UserMapper.ToDtoList(users);
    }
}
