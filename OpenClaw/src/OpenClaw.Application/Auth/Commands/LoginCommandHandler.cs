using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;

using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;
using OpenClaw.Contracts.Auth.Responses;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Auth.Commands;

public class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork uow) : IRequestHandler<LoginCommand, ErrorOr<AuthResponse>>
{
    public async ValueTask<ErrorOr<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, ct);
        if (user is null)
        {
            return UserErrors.InvalidCredentials;
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash.Value))
        {
            return UserErrors.InvalidCredentials;
        }

        if (user.Status == Domain.Users.Enums.UserStatus.Pending)
        {
            return UserErrors.AccountPendingApproval;
        }

        if (user.Status == Domain.Users.Enums.UserStatus.Banned)
        {
            return UserErrors.AccountBanned(user.BanReason);
        }

        if (user.Status != Domain.Users.Enums.UserStatus.Active)
        {
            return UserErrors.AccountNotActive;
        }

        user.RecordLogin();

        var (refreshToken, refreshTokenExpiry) = jwtTokenGenerator.GenerateRefreshToken();
        user.SetRefreshToken(refreshToken, refreshTokenExpiry);

        await userRepository.UpdateAsync(user, ct);
        await uow.SaveChangesAsync(ct);
        
        var token = jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Name,
            user.Email.Value,
            user.Permissions.ToList(),
            user.Roles.ToList());

        return new AuthResponse(
            Token: token,
            RefreshToken: refreshToken,
            Id: user.Id,
            Name: user.Name,
            Email: user.Email.Value,
            Permissions: user.Permissions,
            Roles: user.Roles);
    }
}
