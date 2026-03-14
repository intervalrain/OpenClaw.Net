using Asp.Versioning;

using Mediator;

using Microsoft.AspNetCore.Mvc;

using OpenClaw.Contracts.Pipelines;
using OpenClaw.Contracts.Pipelines.Commands;
using OpenClaw.Contracts.Pipelines.Queries;
using OpenClaw.Contracts.Pipelines.Requests;
using OpenClaw.Contracts.Pipelines.Responses;

using Weda.Core.Presentation;

namespace OpenClaw.Api.Pipelines.Controllers;

[ApiVersion("1.0")]
public class PipelineController(
    ISender sender,
    IPipelineExecutionStore executionStore) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> ListPipelines(CancellationToken ct)
    {
        var result = await sender.Send(new GetPipelinesQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetPipeline(string name, CancellationToken ct)
    {
        var result = await sender.Send(new GetPipelinesQuery(), ct);
        var pipeline = result.Pipelines.FirstOrDefault(p => p.Name == name);
        return pipeline is null ? NotFound() : Ok(pipeline);
    }

    [HttpPost("{name}/execute")]
    public async Task<IActionResult> ExecutePipeline(
        string name,
        [FromBody] ExecutePipelineRequest? request,
        CancellationToken ct)
    {
        var argsJson = request?.Args.HasValue == true
            ? request.Args.Value.GetRawText()
            : null;

        var result = await sender.Send(new ExecutePipelineCommand(name, argsJson), ct);

        return result.Match<IActionResult>(
            executionId => Accepted(new { executionId }),
            errors => Problem(errors));
    }

    [HttpGet("executions")]
    public async Task<IActionResult> ListExecutions([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var executions = await executionStore.ListRecentAsync(limit, ct);
        var response = executions.Select(e => new
        {
            Id = e.Id,
            PipelineName = e.PipelineName,
            Status = e.Status.ToString(),
            StartedAt = e.CreatedAt,
            ErrorMessage = e.Status == PipelineExecutionStatus.Failed ? e.Result?.Summary : null
        });

        return Ok(response);
    }

    [HttpGet("executions/{executionId}")]
    public async Task<IActionResult> GetExecution(string executionId, CancellationToken ct)
    {
        var execution = await executionStore.GetAsync(executionId, ct);
        if (execution is null)
            return NotFound();

        // Return anonymous object to ensure Status is serialized as string
        return Ok(new
        {
            ExecutionId = execution.Id,
            PipelineName = execution.PipelineName,
            Status = execution.Status.ToString(),
            Summary = execution.Result?.Summary,
            Steps = execution.Result?.Steps,
            ApprovalInfo = execution.PendingApproval
        });
    }

    [HttpPost("executions/{executionId}/approve")]
    public async Task<IActionResult> ApproveExecution(string executionId, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitApprovalCommand(executionId, true), ct);
        return result.Match<IActionResult>(
            _ => Ok(),
            errors => Problem(errors));
    }

    [HttpPost("executions/{executionId}/reject")]
    public async Task<IActionResult> RejectExecution(string executionId, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitApprovalCommand(executionId, false), ct);
        return result.Match<IActionResult>(
            _ => Ok(),
            errors => Problem(errors));
    }
}
