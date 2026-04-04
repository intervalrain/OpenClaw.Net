using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Application.CronJobs;
using OpenClaw.Contracts.CronJobs;
using OpenClaw.Contracts.CronJobs.Commands;
using OpenClaw.Contracts.CronJobs.Queries;
using OpenClaw.Contracts.CronJobs.Requests;
using OpenClaw.Contracts.Skills;
using OpenClaw.Domain.CronJobs;
using OpenClaw.Domain.CronJobs.Repositories;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.CronJobs.Controllers;

[ApiVersion("1.0")]
public class CronJobController(
    ISender sender,
    ICurrentUserProvider currentUserProvider,
    IToolRegistry toolRegistry,
    ISkillStore skillStore,
    ICronJobRepository cronJobRepository,
    ICronJobExecutionRepository cronJobExecutionRepository,
    ICronJobExecutor cronJobExecutor) : ApiController
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

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

    [HttpPost("{id:guid}/execute/stream")]
    public async Task ExecuteStream(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            Response.StatusCode = 401;
            return;
        }

        var job = await cronJobRepository.GetByIdAsync(id, ct);
        if (job is null || job.CreatedByUserId != userId)
        {
            Response.StatusCode = 404;
            return;
        }

        // Disable response buffering for SSE
        var bufferingFeature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
        bufferingFeature?.DisableBuffering();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        var channel = Channel.CreateBounded<CronJobStreamEvent>(
            new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

        var executionId = await cronJobExecutor.ExecuteAsync(
            job, userId, ExecutionTrigger.Manual, channel.Writer, ct);

        // Send execution ID as first event
        await WriteEventAsync("executionId", JsonSerializer.Serialize(new { executionId }, JsonOptions), ct);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                var data = JsonSerializer.Serialize(evt, JsonOptions);
                await WriteEventAsync("message", data, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }

    private async Task WriteEventAsync(string eventType, string data, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {eventType}\ndata: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
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

    [HttpDelete("{id:guid}/executions")]
    public async Task<IActionResult> ClearExecutions(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var job = await cronJobRepository.GetByIdAsync(id, ct);
        if (job is null || job.CreatedByUserId != userId) return NotFound();

        await cronJobExecutionRepository.DeleteByCronJobIdAsync(id, ct);
        return NoContent();
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
