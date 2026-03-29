using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Configuration;

using Weda.Core.Application.Security.Models;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[ApiVersion("1.0")]
[Authorize(Policy = Policy.SuperAdminOnly)]
public class AppConfigController(IConfigStore configStore) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var configs = await configStore.GetAllAsync(ct);
        return Ok(configs);
    }

    [HttpGet("{key}")]
    public IActionResult Get(string key)
    {
        var value = configStore.Get(key);
        if (value is null)
            return NotFound(new { key, message = "Configuration not found" });

        return Ok(new { key, value });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(
        string key,
        [FromBody] SetConfigRequest request,
        CancellationToken ct)
    {
        await configStore.SetAsync(key, request.Value, request.IsSecret, ct);
        return Ok(new { key, success = true });
    }

    [HttpDelete("{key}")]
    public async Task<IActionResult> Delete(string key, CancellationToken ct)
    {
        var deleted = await configStore.DeleteAsync(key, ct);
        if (!deleted)
            return NotFound(new { key, message = "Configuration not found" });

        return Ok(new { key, success = true });
    }
}

public record SetConfigRequest(string? Value, bool IsSecret = false);
