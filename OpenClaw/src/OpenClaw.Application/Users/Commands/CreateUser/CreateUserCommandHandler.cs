using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;

using OpenClaw.Application.Users.Mapping;
using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Contracts.Users.Dtos;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Users.Commands.CreateUser;

public class CreateUserCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher) : IRequestHandler<CreateUserCommand, ErrorOr<UserDto>>
{
    public async ValueTask<ErrorOr<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var existingUser = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            return UserErrors.DuplicateEmail;
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);

        var userResult = User.Create(
            request.Email,
            passwordHash,
            request.Name);

        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        var user = userResult.Value;
        await userRepository.AddAsync(user, cancellationToken);

        return UserMapper.ToDto(user);
    }
}
