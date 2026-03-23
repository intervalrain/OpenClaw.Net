using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;
using Weda.Core.Application.Interfaces;

using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Auth.Responses;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

namespace OpenClaw.Application.Auth.Commands;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow) : IRequestHandler<RegisterCommand, ErrorOr<RegisterResponse>>
{
    public async ValueTask<ErrorOr<RegisterResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        // Check if email already exists
        var existingUser = await userRepository.GetByEmailAsync(request.Email, ct);
        if (existingUser is not null)
        {
            return UserErrors.DuplicateEmail;
        }

        // Create user with hashed password and Pending status
        var passwordHash = passwordHasher.HashPassword(request.Password);
        var userResult = User.Create(
            request.Email,
            passwordHash,
            request.Name,
            roles: null,
            permissions: null,
            status: UserStatus.Pending);

        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        var user = userResult.Value;

        // Save user (no token generated - user must wait for approval)
        await userRepository.AddAsync(user, ct);
        await uow.SaveChangesAsync(ct);

        return new RegisterResponse(
            Id: user.Id,
            Email: user.Email.Value,
            Name: user.Name,
            Status: user.Status.ToString(),
            Message: "Registration submitted. Please wait for admin approval.");
    }
}
