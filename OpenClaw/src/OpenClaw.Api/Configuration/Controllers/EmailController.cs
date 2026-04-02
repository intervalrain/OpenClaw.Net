using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Email;

using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[ApiVersion("1.0")]
[Authorize(Policy = Policy.SuperAdminOnly)]
public class EmailController(IEmailService emailService) : ApiController
{
    [HttpPost("test")]
    public async Task<IActionResult> SendTest([FromBody] SendTestEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.To))
            return BadRequest(new { message = "Email address is required." });

        if (!emailService.IsEnabled)
            return BadRequest(new { message = "Email is not enabled. Please configure SMTP settings first." });

        var sent = await emailService.SendAsync(
            request.To,
            "OpenClaw - Test Email",
            """
            <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 480px; margin: 0 auto; padding: 32px;">
                <h2 style="color: #333;">Email Configuration Works!</h2>
                <p style="color: #666;">If you received this email, your SMTP settings are configured correctly.</p>
            </div>
            """,
            ct);

        return sent
            ? Ok(new { message = $"Test email sent to {request.To}" })
            : BadRequest(new { message = "Failed to send email. Check SMTP settings and server logs." });
    }
}

public record SendTestEmailRequest(string To);
