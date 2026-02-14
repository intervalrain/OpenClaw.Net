using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;
using OpenClaw.Contracts.Auth;
using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Auth.Commands.Login;

public class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) : IRequestHandler<LoginCommand, ErrorOr<AuthResponse>>
{
    public async ValueTask<ErrorOr<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            return UserErrors.InvalidCredentials;
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash.Value))
        {
            return UserErrors.InvalidCredentials;
        }

        if (user.Status != Domain.Users.Enums.UserStatus.Active)
        {
            return UserErrors.AccountNotActive;
        }

        user.RecordLogin();
        await userRepository.UpdateAsync(user, cancellationToken);

        var token = jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Name,
            user.Email.Value,
            user.Permissions.ToList(),
            user.Roles.ToList());

        return new AuthResponse(
            Token: token,
            Id: user.Id,
            Name: user.Name,
            Email: user.Email.Value,
            Permissions: user.Permissions,
            Roles: user.Roles);
    }
}
