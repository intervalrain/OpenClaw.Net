using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using AuthorizeAttribute = Microsoft.AspNetCore.Authorization.AuthorizeAttribute;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Application.AgentActivities;
using OpenClaw.Domain.AgentActivities.Repositories;
using OpenClaw.Domain.Users.Repositories;
using Weda.Core.Application.Security;
using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.AgentActivities.Controllers;

[ApiVersion("1.0")]
[Authorize]
public class AgentActivityController(
    IAgentActivityBroadcast broadcast,
    IAgentActivityRepository activityRepository,
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider) : ApiController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// SSE endpoint for real-time agent activity events.
    /// Admin+ sees all users; regular users see only their own activity.
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        var bufferingFeature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
        bufferingFeature?.DisableBuffering();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        var currentUser = currentUserProvider.GetCurrentUser();
        var isAdmin = currentUser.Roles.Contains(Role.Admin) || currentUser.Roles.Contains(Role.SuperAdmin);

        try
        {
            await foreach (var evt in broadcast.SubscribeAsync(ct))
            {
                // Non-admin users only see their own activity
                if (!isAdmin && evt.UserId != currentUser.Id)
                    continue;

                var data = JsonSerializer.Serialize(evt, JsonOptions);
                await Response.WriteAsync($"data: {data}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }

    /// <summary>
    /// Returns current state snapshot — latest activity per active user.
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentState(CancellationToken ct)
    {
        var activities = await activityRepository.GetLatestPerUserAsync(ct);

        var currentUser = currentUserProvider.GetCurrentUser();
        var isAdmin = currentUser.Roles.Contains(Role.Admin) || currentUser.Roles.Contains(Role.SuperAdmin);

        if (!isAdmin)
        {
            activities = activities.Where(a => a.UserId == currentUser.Id).ToList();
        }

        return Ok(activities.Select(a => new
        {
            a.Id,
            a.UserId,
            a.UserName,
            Type = a.Type.ToString(),
            Status = a.Status.ToString(),
            a.SourceId,
            a.SourceName,
            a.Detail,
            a.CreatedAt
        }));
    }

    /// <summary>
    /// Returns historical activities in a time range (for replay mode).
    /// </summary>
    [HttpGet("history")]
    [Authorize(Policy = Policy.AdminOrAbove)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int limit = 1000,
        CancellationToken ct = default)
    {
        if (limit > 5000) limit = 5000;

        var activities = await activityRepository.GetByTimeRangeAsync(from, to, limit, ct);

        return Ok(activities.Select(a => new
        {
            a.Id,
            a.UserId,
            a.UserName,
            Type = a.Type.ToString(),
            Status = a.Status.ToString(),
            a.SourceId,
            a.SourceName,
            a.Detail,
            a.CreatedAt
        }));
    }

    /// <summary>
    /// Returns list of users with their names and IDs (for office layout desk assignment).
    /// </summary>
    [HttpGet("users")]
    [Authorize(Policy = Policy.AdminOrAbove)]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await userRepository.GetAllAsync(ct);

        return Ok(users.Select(u => new
        {
            u.Id,
            u.Name,
            Email = u.Email.Value,
            Status = u.Status.ToString()
        }));
    }
}
