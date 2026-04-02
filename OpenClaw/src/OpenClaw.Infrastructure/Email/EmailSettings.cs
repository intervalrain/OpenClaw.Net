namespace OpenClaw.Infrastructure.Email;

public class EmailSettings
{
    public const string Section = "EmailSettings";

    public bool EnableEmailNotifications { get; set; }
    public string DefaultFromEmail { get; set; } = string.Empty;
    public string DefaultFromName { get; set; } = "OpenClaw";
    public SmtpSettings SmtpSettings { get; set; } = new();
}