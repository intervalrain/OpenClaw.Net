using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ClawOS.Contracts.Configuration;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace ClawOS.Api.Configuration.Controllers;

[ApiVersion("1.0")]
public class UserConfigController(
    IUserConfigStore userConfigStore,
    ICurrentUserProvider currentUserProvider) : ApiController
{
    private Guid GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return Guid.Empty; }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var configs = await userConfigStore.GetAllAsync(userId, ct);
        return Ok(configs);
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var value = await userConfigStore.GetAsync(userId, key, ct);
        if (value is null)
            return NotFound(new { key, message = "User configuration not found" });

        return Ok(new { key, value });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(
        string key,
        [FromBody] SetUserConfigRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await userConfigStore.SetAsync(userId, key, request.Value, request.IsSecret, ct);
        return Ok(new { key, success = true });
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var deleted = await userConfigStore.DeleteAsync(userId, key, ct);
        if (!deleted)
            return NotFound(new { key, message = "User configuration not found" });

        return Ok(new { key, success = true });
    }
}

public record SetUserConfigRequest(string? Value, bool IsSecret = true);
