using ErrorOr;

using FluentValidation;

using Mediator;

using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Auth.Responses;
using OpenClaw.Contracts.Email;
using OpenClaw.Domain.Auth.Entities;
using OpenClaw.Domain.Auth.Errors;
using OpenClaw.Domain.Auth.Repositories;
using OpenClaw.Domain.Users.Errors;
using OpenClaw.Domain.Users.Repositories;

using Weda.Core.Application.Interfaces;

using Weda.Core.Application.Security;
using Weda.Core.Application.Validation;

namespace OpenClaw.Application.Auth.Commands;

public class InitiateRegistrationCommandHandler(
    IUserRepository userRepository,
    IEmailVerificationRepository verificationRepository,
    IEmailService emailService,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow) : IRequestHandler<InitiateRegistrationCommand, ErrorOr<InitiateRegistrationResponse>>
{
    public async ValueTask<ErrorOr<InitiateRegistrationResponse>> Handle(InitiateRegistrationCommand command, CancellationToken cancellationToken)
    {
        var existingUser = await userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (existingUser is not null)
            return UserErrors.DuplicateEmail;

        await verificationRepository.RemoveByEmailAsync(command.Email);

        var passwordHash = passwordHasher.HashPassword(command.Password);
        var code = Random.Shared.Next(100000, 999999).ToString();

        var verification = EmailVerification.Create(
            command.Email,
            command.Name,
            passwordHash,
            code,
            expiry: TimeSpan.FromMinutes(10));
        
        await verificationRepository.AddAsync(verification, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        await emailService.SendVerificationCodeAsync(command.Email, code, cancellationToken);

        return new InitiateRegistrationResponse(
            Email: command.Email, 
            Message: "Verification code sent. Please check your email.");
    }
}

public class InitiateRegistrationCommandValidator : AbstractValidator<InitiateRegistrationCommand>
{
    public InitiateRegistrationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithError(UserErrors.EmptyEmail);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithError(UserErrors.EmptyName);

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithError(AuthErrors.PasswordRequired);

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .WithError(AuthErrors.PasswordTooShort);

        RuleFor(x => x.Password)
            .Must(p => p.Any(char.IsUpper))
            .WithError(AuthErrors.PasswordMissingUppercase);

        RuleFor(x => x.Password)
            .Must(p => p.Any(char.IsLower))
            .WithError(AuthErrors.PasswordMissingLowercase);

        RuleFor(x => x.Password)
            .Must(p => p.Any(char.IsDigit))
            .WithError(AuthErrors.PasswordMissingDigit);

        RuleFor(x => x.Password)
            .Must(p => p.Any(c => !char.IsLetterOrDigit(c)))
            .WithError(AuthErrors.PasswordMissingSpecialChar);
    }   
}