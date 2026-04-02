using ErrorOr;

using Mediator;

using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Auth.Responses;
using OpenClaw.Contracts.Email;
using OpenClaw.Domain.Auth.Errors;
using OpenClaw.Domain.Auth.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Auth.Commands;

public class ResendVerificationCommandHandler(
    IEmailVerificationRepository verificationRepository,
    IEmailService emailService,
    IUnitOfWork uow) : IRequestHandler<ResendVerificationCommand, ErrorOr<ResendVerificationResponse>>
{
    public async ValueTask<ErrorOr<ResendVerificationResponse>> Handle(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        var verification = await verificationRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (verification is null)
            return AuthErrors.VerificationNotFound;
        
        var code = Random.Shared.Next(100000, 999999).ToString();
        verification.ResetCode(code, TimeSpan.FromMinutes(10));

        await uow.SaveChangesAsync(cancellationToken);
        await emailService.SendVerificationCodeAsync(command.Email, code, cancellationToken);

        return new ResendVerificationResponse(
            Email: command.Email,
            Message: "Verification code sent. Please check your email");
    }
}