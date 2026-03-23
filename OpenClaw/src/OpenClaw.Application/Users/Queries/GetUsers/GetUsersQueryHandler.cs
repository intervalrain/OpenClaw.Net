using ErrorOr;

using Mediator;

using OpenClaw.Application.Users.Mapping;
using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Contracts.Users.Queries;
using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Users.Queries.GetUsers;

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
