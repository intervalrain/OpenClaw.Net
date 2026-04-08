using System.Net;
using System.Net.Mail;

using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[ApiVersion("1.0")]
[Authorize(Policy = Policy.SuperAdminOnly)]
public class EmailController : ApiController
{
    /// <summary>
    /// Test SMTP connection using the provided settings (without saving).
    /// Sends a test email to the specified address.
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> SendTest([FromBody] TestEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.To))
            return BadRequest(new { message = "Email address is required." });

        if (string.IsNullOrWhiteSpace(request.SmtpServer))
            return BadRequest(new { message = "SMTP server is required." });

        try
        {
            using var client = new SmtpClient(request.SmtpServer, request.SmtpPort)
            {
                EnableSsl = request.UseSsl,
                Credentials = new NetworkCredential(request.SmtpUsername, request.SmtpPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            var fromEmail = request.FromEmail ?? request.SmtpUsername;
            var fromName = request.FromName ?? "OpenClaw";

            var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "OpenClaw - Test Email",
                Body = """
                    <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                        <h2 style="color: #333;">Email Configuration Works!</h2>
                        <p style="color: #666;">If you received this email, your SMTP settings are configured correctly.</p>
                    </div>
                    """,
                IsBodyHtml = true
            };
            message.To.Add(request.To);

            await client.SendMailAsync(message, ct);
            return Ok(new { message = $"Test email sent to {request.To}" });
        }
        catch (SmtpException ex)
        {
            return BadRequest(new { message = $"SMTP error: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Failed to send email: {ex.Message}" });
        }
    }
}

public record TestEmailRequest(
    string To,
    string SmtpServer,
    int SmtpPort = 587,
    string SmtpUsername = "",
    string SmtpPassword = "",
    bool UseSsl = true,
    string? FromEmail = null,
    string? FromName = null);
