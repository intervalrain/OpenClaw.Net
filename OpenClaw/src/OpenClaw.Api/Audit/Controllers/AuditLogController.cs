using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Domain.Audit.Repositories;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Audit.Controllers;

[ApiVersion("1.0")]
[Authorize(Policy = "SuperAdminOnly")]
public class AuditLogController(IAuditLogRepository repository) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        if (limit > 200) limit = 200;

        var logs = await repository.QueryAsync(userId, action, from, to, limit, offset, ct);

        return Ok(logs.Select(l => new
        {
            l.Id,
            l.UserId,
            l.UserEmail,
            l.Action,
            l.HttpMethod,
            l.Path,
            l.StatusCode,
            l.IpAddress,
            l.UserAgent,
            l.Timestamp
        }));
    }

    [HttpDelete("cleanup")]
    public async Task<IActionResult> Cleanup(
        [FromQuery] int retentionDays = 90,
        CancellationToken ct = default)
    {
        if (retentionDays < 7)
            return BadRequest("Retention must be at least 7 days.");

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await repository.DeleteOlderThanAsync(cutoff, ct);

        return Ok(new { deleted, cutoffDate = cutoff });
    }
}
