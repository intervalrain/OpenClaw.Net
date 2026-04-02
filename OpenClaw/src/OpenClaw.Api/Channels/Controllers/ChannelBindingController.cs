using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Authorize = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Application.Channels;
using OpenClaw.Domain.Channels.Repositories;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Channels.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class ChannelBindingController(
    ChannelLinkService linkService,
    IChannelUserBindingRepository repository,
    ICurrentUserProvider currentUserProvider) : ApiController
{
    /// <summary>
    /// Get all channel bindings for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyBindings(CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var bindings = await repository.GetByOpenClawUserAsync(user.Id, ct);

        return Ok(bindings.Select(b => new
        {
            b.Id,
            b.Platform,
            b.ExternalUserId,
            b.DisplayName,
            b.CreatedAt
        }));
    }

    /// <summary>
    /// Verify a code from Telegram /link and create the binding.
    /// </summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyLinkRequest request, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var binding = await linkService.VerifyAndLinkAsync(request.Code, user.Id, ct);

        if (binding is null)
            return BadRequest("Invalid or expired verification code.");

        return Ok(new
        {
            binding.Platform,
            binding.ExternalUserId,
            binding.DisplayName,
            Message = "Channel account linked successfully."
        });
    }

    /// <summary>
    /// Unlink a channel binding.
    /// </summary>
    [HttpDelete("{platform}/{externalUserId}")]
    public async Task<IActionResult> Unlink(string platform, string externalUserId, CancellationToken ct)
    {
        var user = currentUserProvider.GetCurrentUser();
        var result = await linkService.UnlinkAsync(user.Id, platform, externalUserId, ct);

        if (!result)
            return NotFound("Binding not found or not owned by you.");

        return Ok(new { Message = "Channel account unlinked." });
    }
}

public record VerifyLinkRequest(string Code);
