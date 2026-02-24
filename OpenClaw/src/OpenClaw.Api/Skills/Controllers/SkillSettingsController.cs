using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Skills;

using Weda.Core.Presentation;

namespace OpenClaw.Api.Skills.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
public class SkillSettingsController(ISkillSettingsService skillSettingsService) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var settings = await skillSettingsService.GetListAsync(ct);
        return Ok(settings);
    }

    [HttpPost("{skillName}/enable")]
    public async Task<IActionResult> Enable(string skillName, CancellationToken ct)
    {
        await skillSettingsService.EnableAsync(skillName, ct);
        return NoContent();
    }

    [HttpPost("{skillName}/disable")]
    public async Task<IActionResult> Disable(string skillName, CancellationToken ct)
    {
        await skillSettingsService.DisableAsync(skillName, ct);
        return NoContent();
    }
}