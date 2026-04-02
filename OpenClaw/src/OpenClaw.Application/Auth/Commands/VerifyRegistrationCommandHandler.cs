using ErrorOr;

using Mediator;

using OpenClaw.Contracts.Auth.Commands;
using OpenClaw.Contracts.Auth.Responses;
using OpenClaw.Contracts.Email;
using OpenClaw.Domain.Auth.Errors;
using OpenClaw.Domain.Auth.Repositories;
using OpenClaw.Domain.Notifications.Entities;
using OpenClaw.Domain.Notifications.Repositories;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Enums;
using OpenClaw.Domain.Users.Repositories;
using OpenClaw.Domain.Workspaces.Entities;
using OpenClaw.Domain.Workspaces.Repositories;

using Weda.Core.Application.Interfaces;
using Weda.Core.Application.Security.Models;

namespace OpenClaw.Application.Auth.Commands;

public class VerifyRegistrationCommandHandler(
    IEmailVerificationRepository verificationRepository,
    IUserRepository userRepository,
    IWorkspaceRepository workspaceRepository,
    INotificationRepository notificationRepository,
    IEmailService emailService,
    IUnitOfWork uow) : IRequestHandler<VerifyRegistrationCommand, ErrorOr<VerifyRegistrationResponse>>
{
    public async ValueTask<ErrorOr<VerifyRegistrationResponse>> Handle(VerifyRegistrationCommand command, CancellationToken cancellationToken)
    {
        var verification = await verificationRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (verification is null)
            return AuthErrors.VerificationNotFound;

        if (verification.HasExceededMaxAttempts())
            return AuthErrors.VerificationMaxAttempts;

        if (!verification.TryVerify(command.Code))
        {
            await uow.SaveChangesAsync(cancellationToken);
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

        // Notify admins (toast + email)
        await NotifyAdminsAsync(user, command.BaseUrl, cancellationToken);

        await uow.SaveChangesAsync(cancellationToken);

        return new VerifyRegistrationResponse(
            Id: user.Id,
            Email: user.Email.Value,
            Name: user.Name,
            Status: user.Status.ToString(),
            Message: "Registration submitted. Please wait for admin approval");
    }

    private async Task NotifyAdminsAsync(User newUser, string baseUrl, CancellationToken ct)
    {
        var allUsers = await userRepository.GetAllAsync(ct);
        var admins = allUsers.Where(u =>
            u.Roles.Contains(Role.Admin) || u.Roles.Contains(Role.SuperAdmin)).ToList();

        // Create in-app notifications for each admin
        foreach (var admin in admins)
        {
            var notification = Notification.Create(
                admin.Id,
                "New User Registration",
                $"{newUser.Name} ({newUser.Email.Value}) has registered and is awaiting approval.",
                type: "info",
                link: "/admin/index.html");

            await notificationRepository.AddAsync(notification, ct);
        }

        // Send email notification
        var adminLink = $"{baseUrl.TrimEnd('/')}/admin/index.html";
        var emailHtml = $"""
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                <h2 style="color: #333; margin-bottom: 8px;">New User Registration</h2>
                <p style="color: #666; font-size: 14px;">A new user has registered and is awaiting your approval:</p>
                <div style="background: #f0f4f8; border-radius: 8px; padding: 16px; margin: 16px 0;">
                    <p style="margin: 4px 0;"><strong>Name:</strong> {newUser.Name}</p>
                    <p style="margin: 4px 0;"><strong>Email:</strong> {newUser.Email.Value}</p>
                </div>
                <a href="{adminLink}" style="display: inline-block; margin-top: 12px; padding: 10px 20px; background: #3498db; color: white; text-decoration: none; border-radius: 6px; font-size: 14px; font-weight: 500;">Review in Admin Panel</a>
                <p style="color: #999; font-size: 12px; margin-top: 12px;">Or visit the admin panel directly to approve or reject this registration.</p>
            </div>
            """;

        await emailService.SendAdminNotificationAsync(
            $"New Registration: {newUser.Name}",
            emailHtml,
            ct);
    }
}
