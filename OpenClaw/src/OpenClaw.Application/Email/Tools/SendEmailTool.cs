using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using OpenClaw.Contracts.Email;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Email.Tools;

public class SendEmailTool(IServiceScopeFactory scopeFactory) : AgentToolBase<SendEmailArgs>
{
    public override string Name => "send_email";
    public override string Description =>
        "Send an email. Requires SMTP to be configured by an admin. " +
        "Use this to deliver reports, notifications, or summaries to users.";

    public override async Task<ToolResult> ExecuteAsync(SendEmailArgs args, ToolContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.To))
            return ToolResult.Failure("Recipient email address (to) is required.");
        if (string.IsNullOrWhiteSpace(args.Subject))
            return ToolResult.Failure("Subject is required.");
        if (string.IsNullOrWhiteSpace(args.Body))
            return ToolResult.Failure("Body is required.");

        using var scope = scopeFactory.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        if (!emailService.IsEnabled)
            return ToolResult.Failure("Email is not configured. Ask an admin to set up SMTP in Application Settings.");

        var sent = await emailService.SendAsync(args.To, args.Subject, args.Body, ct);

        return sent
            ? ToolResult.Success($"Email sent to {args.To} with subject \"{args.Subject}\".")
            : ToolResult.Failure($"Failed to send email to {args.To}. Check server logs for SMTP errors.");
    }
}

public record SendEmailArgs(
    [property: Description("Recipient email address")]
    string To,

    [property: Description("Email subject line")]
    string Subject,

    [property: Description("Email body content (HTML supported)")]
    string Body);
