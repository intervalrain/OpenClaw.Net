using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Channels.Teams.Adapters;
using OpenClaw.Channels.Teams.Handlers;

namespace OpenClaw.Channels.Teams.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teams")]
[ApiController]
public class TeamsController(TeamsChannelAdapter adapter) : ControllerBase
{
    /// <summary>
    /// Bot Framework messaging endpoint.
    /// Teams sends POST requests here with Activity payloads.
    /// Authentication is handled by the BotFrameworkAdapter (JWT validation).
    /// </summary>
    [HttpPost("messages")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleMessage(CancellationToken ct)
    {
        if (adapter.Adapter is null || adapter.ActivityHandler is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Teams bot is not running." });

        await adapter.Adapter.ProcessAsync(
            Request,
            Response,
            adapter.ActivityHandler,
            ct);

        return new EmptyResult();
    }
}
