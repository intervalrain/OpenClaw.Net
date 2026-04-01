using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using ClawOS.Contracts.CronJobs.Commands;
using ClawOS.Contracts.CronJobs.Queries;
using ClawOS.Contracts.CronJobs.Requests;
using ClawOS.Contracts.Skills;
using ClawOS.Domain.CronJobs;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace ClawOS.Api.CronJobs.Controllers;

[ApiVersion("1.0")]
public class CronJobController(
    ISender sender,
    ICurrentUserProvider currentUserProvider,
    IToolRegistry toolRegistry,
    ISkillStore skillStore) : ApiController
{
    private Guid GetUserId()
    {
        try { return currentUserProvider.GetCurrentUser().Id; }
        catch { return Guid.Empty; }
    }

    // === CRUD ===

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetCronJobsQuery(userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetCronJobQuery(id, userId), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCronJobRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new CreateCronJobCommand(
            request.Name, request.ScheduleJson, request.SessionId,
            request.WakeMode, request.ContextJson, request.Content, userId), ct);

        return result.Match<IActionResult>(
            job => CreatedAtAction(nameof(Get), new { id = job.Id }, job),
            errors => Problem(errors));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCronJobRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new UpdateCronJobCommand(
            id, request.Name, request.ScheduleJson, request.SessionId,
            request.WakeMode, request.ContextJson, request.Content, request.IsActive, userId), ct);

        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new DeleteCronJobCommand(id, userId), ct);
        return result.Match<IActionResult>(_ => NoContent(), errors => Problem(errors));
    }

    // === Execution ===

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await sender.Send(new ExecuteCronJobCommand(id, userId), ct);
        return result.Match<IActionResult>(
            executionId => Accepted(new { executionId }),
            errors => Problem(errors));
    }

    [HttpGet("executions")]
    public async Task<IActionResult> ListExecutions(
        [FromQuery] Guid? cronJobId, [FromQuery] int limit = 20, [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var result = await sender.Send(new GetCronJobExecutionsQuery(userId, cronJobId, limit, offset), ct);
        return result.Match<IActionResult>(Ok, errors => Problem(errors));
    }

    // === Skills & Tools (for editor autocomplete) ===

    [HttpGet("skills")]
    public IActionResult ListSkills()
    {
        var skills = skillStore.GetAllSkills()
            .Select(s => new { s.Name, s.Description, s.Tools })
            .OrderBy(s => s.Name)
            .ToList();
        return Ok(skills);
    }

    [HttpGet("tools")]
    public IActionResult ListTools()
    {
        var tools = toolRegistry.GetAllSkills()
            .Select(t => new { t.Name, t.Description, Parameters = t.Parameters })
            .OrderBy(t => t.Name)
            .ToList();
        return Ok(tools);
    }
}
