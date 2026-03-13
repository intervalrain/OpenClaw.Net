using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Users.Commands;
using OpenClaw.Contracts.Users.Queries;

using Weda.Core.Presentation;

namespace OpenClaw.Api.Users.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class UserPreferenceController(ISender mediator) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var query = new ListUserPreferencesQuery();
        var result = await mediator.Send(query, ct);

        return result.Match(Ok, Problem);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var query = new GetUserPreferenceQuery(key);
        var result = await mediator.Send(query, ct);

        return result.Match(
            preference => preference is null
                ? NotFound(new { key, message = "Preference not found" })
                : Ok(preference),
            Problem);
    }

    [HttpGet("prefix/{prefix}")]
    public async Task<IActionResult> GetByPrefix(string prefix, CancellationToken ct)
    {
        var query = new ListUserPreferencesQuery(prefix);
        var result = await mediator.Send(query, ct);

        return result.Match(Ok, Problem);
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(
        string key,
        [FromBody] SetPreferenceRequest request,
        CancellationToken ct)
    {
        var command = new SetUserPreferenceCommand(key, request.Value);
        var result = await mediator.Send(command, ct);

        return result.Match(
            _ => Ok(new { key, success = true }),
            Problem);
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        var command = new DeleteUserPreferenceCommand(key);
        var result = await mediator.Send(command, ct);

        return result.Match(
            _ => Ok(new { key, success = true }),
            Problem);
    }
}

public record SetPreferenceRequest(string? Value);
