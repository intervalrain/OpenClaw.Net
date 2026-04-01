using ErrorOr;

using Mediator;

using ClawOS.Application.Users.Mapping;
using ClawOS.Contracts.Users.Dtos;
using ClawOS.Contracts.Users.Queries;
using ClawOS.Domain.Users.Errors;
using ClawOS.Domain.Users.Repositories;

namespace ClawOS.Application.Users.Queries.GetUser;

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
