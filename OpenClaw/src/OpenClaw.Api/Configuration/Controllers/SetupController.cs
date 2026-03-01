using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Application.Setup.Queries;
using OpenClaw.Contracts.Configuration.Requests;
using OpenClaw.Contracts.Setup.Commands;

using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
public class SetupController(ISender sender) : ApiController
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var query = new GetSetupStatusQuery();
        var result = await sender.Send(query, ct);

        return result.Match(Ok, Problem);
    }

    [HttpPost("init")]
    public async Task<IActionResult> Initialize([FromBody] InitRequest request, CancellationToken ct)
    {
        var command = new InitializeSystemCommand(request.Email, request.Password, request.Name);
        var result = await sender.Send(command, ct);

        return result.Match(Ok, Problem);
    }
}