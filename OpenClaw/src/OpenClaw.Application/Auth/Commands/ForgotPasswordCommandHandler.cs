using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Email;
using OpenClaw.Domain.Auth.Entities;
using OpenClaw.Domain.Auth.Repositories;
using OpenClaw.Domain.Users.Repositories;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Auth.Commands;

public class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordResetTokenRepository tokenRepository,
    IEmailService emailService,
    IUnitOfWork uow) : IRequestHandler<ForgotPasswordCommand, ErrorOr<ForgotPasswordResponse>>
{
    public async ValueTask<ErrorOr<ForgotPasswordResponse>> Handle(
        ForgotPasswordCommand command, CancellationToken ct)
    {
        // Always return success to prevent email enumeration
        var successResponse = new ForgotPasswordResponse(
            "If an account with that email exists, a password reset link has been sent.");

        var user = await userRepository.GetByEmailAsync(command.Email, ct);
        if (user is null)
            return successResponse;

        // Invalidate previous tokens
        await tokenRepository.InvalidateAllForEmailAsync(command.Email, ct);

        // Create new token (30 min expiry)
        var resetToken = PasswordResetToken.Create(command.Email, TimeSpan.FromMinutes(30));
        await tokenRepository.AddAsync(resetToken, ct);
        await uow.SaveChangesAsync(ct);

        // Send reset email
        var resetLink = $"{command.BaseUrl.TrimEnd('/')}/reset-password.html?token={resetToken.Token}";
        var html = $"""
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                <h2 style="color: #333; margin-bottom: 8px;">Reset Your Password</h2>
                <p style="color: #666; font-size: 14px;">Click the button below to reset your password:</p>
                <a href="{resetLink}" style="display: inline-block; margin: 20px 0; padding: 12px 24px; background: #3498db; color: white; text-decoration: none; border-radius: 6px; font-size: 14px; font-weight: 600;">Reset Password</a>
                <p style="color: #999; font-size: 12px;">This link expires in 30 minutes. If you didn't request this, you can safely ignore this email.</p>
            </div>
            """;

        await emailService.SendAsync(command.Email, "OpenClaw - Password Reset", html, ct);

        return successResponse;
    }
}
