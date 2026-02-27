using ErrorOr;

using OpenClaw.Domain.Users.Entities;

using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;

namespace OpenClaw.Contracts.Setup.Commands;

public record InitializeSystemCommand(string Email, string Password, string? Name) : IQuery<ErrorOr<InitializeSystemResult>>;

public record InitializeSystemResult(string Message);

public class InitializeSystemCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow) 
    : Mediator.IRequestHandler<InitializeSystemCommand, ErrorOr<InitializeSystemResult>>
{
    public async ValueTask<ErrorOr<InitializeSystemResult>> Handle(InitializeSystemCommand request, CancellationToken ct)
    {
        var hasUser = await userRepository.AnyAsync(ct);

        if (hasUser) return Error.Conflict("Setup.AlreadyInitialized", "System already initialized");

        var userResult = User.Create(
            email: request.Email,
            passwordHash: passwordHasher.HashPassword(request.Password),
            name: request.Name ?? "Admin");

        if (userResult.IsError)
        {
            return userResult.Errors;
        }

        await userRepository.AddAsync(userResult.Value, ct);
        await uow.SaveChangesAsync(ct);

        return new InitializeSystemResult("User created successfully");
    }
}