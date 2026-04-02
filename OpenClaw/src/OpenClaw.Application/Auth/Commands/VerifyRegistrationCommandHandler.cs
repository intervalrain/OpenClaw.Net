using ErrorOr;

using Mediator;

using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Auth.Responses;
using OpenClaw.Domain.Auth.Errors;
using OpenClaw.Domain.Auth.Repositories;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.Repositories;
using OpenClaw.Domain.Workspaces.Entities;
using OpenClaw.Domain.Workspaces.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Auth.Commands;

public class VerifyRegistrationCommandHandler(
    IEmailVerificationRepository verificationRepository,
    IUserRepository userRepository,
    IWorkspaceRepository workspaceRepository,
    IUnitOfWork uow) : IRequestHandler<VerifyRegistrationCommand, ErrorOr<VerifyRegistrationResponse>>
{
    public async ValueTask<ErrorOr<VerifyRegistrationResponse>> Handle(VerifyRegistrationCommand command, CancellationToken cancellationToken)
    {
        var verification = await verificationRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (verification is null)
            return AuthErrors.VerificationNotFound;

        if (verification.HasExceededMaxAttempts())
            return AuthErrors.VerificationMaxAttempts;

        if (!verification.TryVerify(command.Code)) {
            await uow.SaveChangesAsync(cancellationToken);  // persist attempt count
            return AuthErrors.VerificationCodeInvalid;
        }

        var userResult = User.Create(
            verification.Email,
            verification.PasswordHash,
            verification.Name,
            roles: null,
            permissions: null,
            status: UserStatus.Pending);

        if (userResult.IsError)
            return userResult.Errors;

        var user = userResult.Value;
        await userRepository.AddAsync(user, cancellationToken);

        var workspace = Workspace.CreatePersonal(user.Id, user.Name);
        await workspaceRepository.AddAsync(workspace, cancellationToken);

        await verificationRepository.RemoveByEmailAsync(command.Email, cancellationToken);

        await uow.SaveChangesAsync(cancellationToken);

        return new VerifyRegistrationResponse(
            Id: user.Id,
            Email: user.Email.Value,
            Name: user.Name,
            Status: user.Status.ToString(),
            Message: "Registration submitted. Please wait for admin approval");
    }
}