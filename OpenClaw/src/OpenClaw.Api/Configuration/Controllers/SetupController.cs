using Asp.Versioning;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using OpenClaw.Domain.Configuration.Repositories;

using Weda.Core.Presentation;

namespace OpenClaw.Api.Configuration.Controllers;

[AllowAnonymous]
[ApiVersion("1.0")]
public class SetupController(IModelProviderRepository repository) : ApiController
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var activeProvider = await repository.GetActiveAsync(ct);

        return Ok(new
        {
            isConfigured = activeProvider is not null,
            activeProvider = activeProvider is null ? null : new
            {
                activeProvider.Id,
                activeProvider.Type,
                activeProvider.Name,
                activeProvider.ModelName
            } 
        });
    }
}