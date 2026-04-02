using ErrorOr;
using FluentValidation;
using Mediator;
using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Domain.Auth.Errors;
using OpenClaw.Domain.Auth.Repositories;
using OpenClaw.Domain.Users.Repositories;
using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security;
using Weda.Core.Application.Validation;

namespace OpenClaw.Application.Auth.Commands;

public class ResetPasswordCommandHandler(
    IPasswordResetTokenRepository tokenRepository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork uow) : IRequestHandler<ResetPasswordCommand, ErrorOr<ResetPasswordResponse>>
{
    public async ValueTask<ErrorOr<ResetPasswordResponse>> Handle(
        ResetPasswordCommand command, CancellationToken ct)
    {
        var resetToken = await tokenRepository.GetByTokenAsync(command.Token, ct);
        if (resetToken is null)
            return AuthErrors.ResetTokenInvalid;

        if (resetToken.IsUsed)
            return AuthErrors.ResetTokenUsed;

        if (!resetToken.IsValid())
            return AuthErrors.ResetTokenInvalid;

        var user = await userRepository.GetByEmailAsync(resetToken.Email, ct);
        if (user is null)
            return AuthErrors.ResetTokenInvalid;

        // Update password
        var newHash = passwordHasher.HashPassword(command.NewPassword);
        user.UpdatePassword(newHash);

        // Mark token as used
        resetToken.MarkUsed();

        await uow.SaveChangesAsync(ct);

        return new ResetPasswordResponse("Password has been reset successfully. You can now sign in.");
    }
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty()
            .WithError(AuthErrors.ResetTokenInvalid);

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithError(AuthErrors.PasswordRequired);

        RuleFor(x => x.NewPassword)
            .MinimumLength(8)
            .WithError(AuthErrors.PasswordTooShort);

        RuleFor(x => x.NewPassword)
            .Must(p => p.Any(char.IsUpper))
            .WithError(AuthErrors.PasswordMissingUppercase);

        RuleFor(x => x.NewPassword)
            .Must(p => p.Any(char.IsLower))
            .WithError(AuthErrors.PasswordMissingLowercase);

        RuleFor(x => x.NewPassword)
            .Must(p => p.Any(char.IsDigit))
            .WithError(AuthErrors.PasswordMissingDigit);

        RuleFor(x => x.NewPassword)
            .Must(p => p.Any(c => !char.IsLetterOrDigit(c)))
            .WithError(AuthErrors.PasswordMissingSpecialChar);
    }
}
