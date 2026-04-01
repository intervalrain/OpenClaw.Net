using ErrorOr;

using ClawOS.Contracts.Auth.Responses;
using ClawOS.Domain.Users.Errors;
using ClawOS.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;

namespace ClawOS.Application.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IQuery<ErrorOr<AuthResponse>>;

public class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IUnitOfWork uow) : Mediator.IRequestHandler<RefreshTokenCommand, ErrorOr<AuthResponse>>
{
    public async ValueTask<ErrorOr<AuthResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (user is null || !user.IsRefreshTokenValid(request.RefreshToken))
        {
            return UserErrors.InvalidRefreshToken;
        }

        var (newRefreshToken, refreshTokenExpiry) = jwtTokenGenerator.GenerateRefreshToken();
        user.SetRefreshToken(newRefreshToken, refreshTokenExpiry);

        await userRepository.UpdateAsync(user, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        var token = jwtTokenGenerator.GenerateToken(
            user.Id,
            user.Name,
            user.Email.Value,
            user.Permissions.ToList(),
            user.Roles.ToList());

        return new AuthResponse(
            Token: token,
            RefreshToken: newRefreshToken,
            Id: user.Id,
            Name: user.Name,
            Email: user.Email.Value,
            Permissions: user.Permissions,
            Roles: user.Roles);
    }
}