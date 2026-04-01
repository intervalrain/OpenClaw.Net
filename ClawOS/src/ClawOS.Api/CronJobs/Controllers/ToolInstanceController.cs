using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using ClawOS.Contracts.CronJobs.Commands;
using ClawOS.Contracts.CronJobs.Queries;
using ClawOS.Contracts.CronJobs.Requests;
using ClawOS.Contracts.Configuration;
using ClawOS.Domain.Users.Repositories;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace ClawOS.Api.CronJobs.Controllers;

[ApiVersion("1.0")]
public class ToolInstanceController(
    ISender sender,
    ICurrentUserProvider currentUserProvider,
    IConfigStore configStore,
    IUserConfigStore userConfigStore,
    IUserPreferenceRepository userPreferenceRepository) : ApiController
{
    private Guid GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return Guid.Empty; }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetToolInstancesQuery(userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetToolInstancesQuery(GetUserId()), ct);
        return result.Match<IActionResult>(
            instances => Ok(instances.FirstOrDefault(i => i.Id == id)),
            errors => Problem(errors));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateToolInstanceRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new CreateToolInstanceCommand(
            request.Name, request.ToolName, request.ArgsJson, request.Description, userId), ct);

        return result.Match<IActionResult>(
            instance => CreatedAtAction(nameof(Get), new { id = instance.Id }, instance),
            errors => Problem(errors));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateToolInstanceRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UpdateToolInstanceCommand(
            id, request.Name, request.ToolName, request.ArgsJson, request.Description, userId), ct);

        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new DeleteToolInstanceCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    /// <summary>
    /// Auto-fill arg values from ConfigStore and UserPreference.
    /// </summary>
    [HttpPost("args/suggest")]
    public async Task<IActionResult> SuggestArgValues([FromBody] List<string> keys, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = new Dictionary<string, string?>();

        foreach (var key in keys)
        {
            // Priority 1: AppConfig (global)
            var configValue = configStore.Get(key);
            if (configValue is not null) { result[key] = configValue; continue; }

            if (userId != Guid.Empty)
            {
                // Priority 2: UserConfig (per-user, encrypted)
                var userConfigValue = await userConfigStore.GetAsync(userId, key, ct);
                if (userConfigValue is not null) { result[key] = userConfigValue; continue; }

                // Priority 3: UserPreference (per-user, plain)
                var pref = await userPreferenceRepository.GetByKeyAsync(userId, key, ct);
                if (pref?.Value is not null) { result[key] = pref.Value; continue; }
            }
        }

        return Ok(result);
    }
}
