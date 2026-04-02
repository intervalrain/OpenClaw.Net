namespace OpenClaw.Contracts.Email;

public interface IEmailService
{
    Task<bool> SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task<bool> SendVerificationCodeAsync(string to, string code, CancellationToken ct = default);
    Task<bool> SendAdminNotificationAsync(string subject, string htmlBody, CancellationToken ct = default);
    bool IsEnabled { get; }
}
