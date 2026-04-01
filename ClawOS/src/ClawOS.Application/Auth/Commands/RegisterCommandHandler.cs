using ErrorOr;

using Mediator;

using Weda.Core.Application.Security;
using Weda.Core.Application.Interfaces;

using ClawOS.Contracts.Auth.Commands;
using ClawOS.Contracts.Auth.Responses;
using ClawOS.Domain.Users.Entities;
using ClawOS.Domain.Users.Enums;
using ClawOS.Domain.Users.Errors;
using ClawOS.Domain.Users.Repositories;

namespace ClawOS.Application.Auth.Commands;

public class RegisterCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow) : IRequestHandler<RegisterCommand, ErrorOr<RegisterResponse>>
{
    public async ValueTask<ErrorOr<RegisterResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        // Password complexity validation
        var passwordError = ValidatePassword(request.Password);
        if (passwordError is not null)
        {
            return Error.Validation("Password.TooWeak", passwordError);
        }

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

    private static string? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Password is required.";

        if (password.Length < 12)
            return "Password must be at least 12 characters long.";

        if (!password.Any(char.IsUpper))
            return "Password must contain at least one uppercase letter.";

        if (!password.Any(char.IsLower))
            return "Password must contain at least one lowercase letter.";

        if (!password.Any(char.IsDigit))
            return "Password must contain at least one digit.";

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
            return "Password must contain at least one special character.";

        return null;
    }
}
