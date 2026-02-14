using ErrorOr;

using Mediator;

using OpenClaw.Application.Users.Mapping;
using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Contracts.Users.Queries;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Users.Queries.GetUser;

public class GetUserQueryHandler(
    IUserRepository userRepository) : IRequestHandler<GetUserQuery, ErrorOr<UserDto>>
{
    public async ValueTask<ErrorOr<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        return UserMapper.ToDto(user);
    }
}
