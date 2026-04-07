using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenClaw.Contracts.Configuration;
using OpenClaw.Contracts.Email;
using OpenClaw.Domain.Users.Repositories;
using Weda.Core.Application.Security.Models;

namespace OpenClaw.Infrastructure.Email;

public class SmtpEmailService(
    IConfigStore configStore,
    IOptions<EmailSettings> options,
    IUserRepository userRepository,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private const string KeyPrefix = "Email:";
    private readonly EmailSettings _fallback = options.Value;

    public bool IsEnabled
    {
        get
        {
            var enabled = configStore.Get($"{KeyPrefix}Enabled");
            if (enabled is not null)
                return string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase);

            // If SMTP server is configured in DB, consider email enabled
            var dbServer = configStore.Get($"{KeyPrefix}SmtpServer");
            if (!string.IsNullOrEmpty(dbServer))
                return true;

            return _fallback.EnableEmailNotifications
                && !string.IsNullOrEmpty(_fallback.SmtpSettings.Server);
        }
    }

    public async Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("Email not sent (disabled): {Subject} -> {To}", subject, to);
            return false;
        }

        try
        {
            using var client = CreateSmtpClient();
            var fromEmail = configStore.Get($"{KeyPrefix}FromEmail") ?? _fallback.DefaultFromEmail;
            var fromName = configStore.Get($"{KeyPrefix}FromName") ?? _fallback.DefaultFromName;

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message, ct);
            logger.LogInformation("Email sent: {Subject} -> {To}", subject, to);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email: {Subject} -> {To}", subject, to);
            return false;
        }
    }

    public async Task<bool> SendVerificationCodeAsync(string to, string code, CancellationToken ct = default)
    {
        var html = $"""
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                <h2 style="color: #333; margin-bottom: 8px;">Verify Your Email</h2>
                <p style="color: #666; font-size: 14px;">Enter this code to complete your OpenClaw registration:</p>
                <div style="background: #f0f4f8; border-radius: 8px; padding: 20px; text-align: center; margin: 24px 0;">
                    <span style="font-size: 32px; font-weight: 700; letter-spacing: 8px; color: #1a1a1a;">{code}</span>
                </div>
                <p style="color: #999; font-size: 12px;">This code expires in 10 minutes. If you didn't request this, ignore this email.</p>
            </div>
            """;

        return await SendAsync(to, "OpenClaw - Email Verification Code", html, ct);
    }

    public async Task<bool> SendAdminNotificationAsync(string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsEnabled) return false;

        var users = await userRepository.GetAllAsync(ct);
        var admins = users.Where(u =>
            u.Roles.Contains(Role.Admin) || u.Roles.Contains(Role.SuperAdmin));

        var sent = false;
        foreach (var admin in admins)
        {
            sent |= await SendAsync(admin.Email.Value, subject, htmlBody, ct);
        }
        return sent;
    }

    private SmtpClient CreateSmtpClient()
    {
        var server = configStore.Get($"{KeyPrefix}SmtpServer") ?? _fallback.SmtpSettings.Server;
        var port = int.TryParse(configStore.Get($"{KeyPrefix}SmtpPort"), out var p) ? p : _fallback.SmtpSettings.Port;
        var username = configStore.Get($"{KeyPrefix}SmtpUsername") ?? _fallback.SmtpSettings.Username;
        var password = configStore.Get($"{KeyPrefix}SmtpPassword") ?? _fallback.SmtpSettings.Password;
        var useSsl = configStore.Get($"{KeyPrefix}SmtpUseSsl") is { } ssl
            ? string.Equals(ssl, "true", StringComparison.OrdinalIgnoreCase)
            : _fallback.SmtpSettings.UseSsl;

        return new SmtpClient(server, port)
        {
            EnableSsl = useSsl,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
    }
}
