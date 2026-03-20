using Asp.Versioning;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Contracts.Workflows.Commands;
using OpenClaw.Contracts.Workflows.Queries;
using OpenClaw.Contracts.Workflows.Requests;
using OpenClaw.Domain.Workflows;
using Weda.Core.Application.Security;
using Weda.Core.Presentation;

namespace OpenClaw.Api.Workflows.Controllers;

[ApiVersion("1.0")]
public class WorkflowController(
    ISender sender,
    ICurrentUserProvider currentUserProvider,
    ISkillRegistry skillRegistry) : ApiController
{
    private Guid GetUserId()
    {
        try
        {
            return currentUserProvider.GetCurrentUser().Id;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    // === Workflow Definition CRUD ===

    [HttpGet]
    public async Task<IActionResult> ListWorkflows(
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await sender.Send(new GetWorkflowsQuery(userId == Guid.Empty ? null : userId, isActive), ct);

        return result.Match<IActionResult>(
            workflows => Ok(workflows),
            errors => Problem(errors));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWorkflow(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkflowQuery(id), ct);

        return result.Match<IActionResult>(
            workflow => Ok(workflow),
            errors => Problem(errors));
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkflow(
        [FromBody] CreateWorkflowRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new CreateWorkflowCommand(
            request.Name,
            request.Description,
            request.Definition,
            request.Schedule,
            userId), ct);

        return result.Match<IActionResult>(
            workflow => CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow),
            errors => Problem(errors));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateWorkflow(
        Guid id,
        [FromBody] UpdateWorkflowRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new UpdateWorkflowCommand(
            id,
            request.Name,
            request.Description,
            request.Definition,
            request.Schedule,
            request.IsActive,
            userId), ct);

        return result.Match<IActionResult>(
            workflow => Ok(workflow),
            errors => Problem(errors));
    }

    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> CloneWorkflow(
        Guid id,
        [FromBody] CloneWorkflowRequest? request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new CloneWorkflowCommand(id, request?.Name, userId), ct);

        return result.Match<IActionResult>(
            workflow => CreatedAtAction(nameof(GetWorkflow), new { id = workflow.Id }, workflow),
            errors => Problem(errors));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteWorkflow(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var result = await sender.Send(new DeleteWorkflowCommand(id, userId), ct);

        return result.Match<IActionResult>(
            _ => NoContent(),
            errors => Problem(errors));
    }

    // === Execution ===

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> ExecuteWorkflow(
        Guid id,
        [FromBody] ExecuteWorkflowRequest? request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        var result = await sender.Send(new ExecuteWorkflowCommand(
            id,
            request?.InputJson,
            request?.VariableOverrides,
            ExecutionTrigger.Manual,
            userId == Guid.Empty ? null : userId), ct);

        return result.Match<IActionResult>(
            executionId => Accepted(new { executionId }),
            errors => Problem(errors));
    }

    [HttpGet("executions")]
    public async Task<IActionResult> ListExecutions(
        [FromQuery] Guid? workflowId,
        [FromQuery] int limit = 20,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new GetWorkflowExecutionsQuery(workflowId, limit, offset), ct);

        return result.Match<IActionResult>(
            executions => Ok(executions),
            errors => Problem(errors));
    }

    [HttpGet("executions/{executionId:guid}")]
    public async Task<IActionResult> GetExecution(Guid executionId, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkflowExecutionQuery(executionId), ct);

        return result.Match<IActionResult>(
            execution => Ok(execution),
            errors => Problem(errors));
    }

    [HttpGet("executions/{executionId:guid}/nodes")]
    public async Task<IActionResult> GetNodeStatuses(Guid executionId, CancellationToken ct)
    {
        var result = await sender.Send(new GetWorkflowExecutionQuery(executionId), ct);

        return result.Match<IActionResult>(
            execution => Ok(new
            {
                Status = execution.Status.ToString(),
                Nodes = execution.NodeExecutions.Select(n => new
                {
                    n.NodeId,
                    n.NodeLabel,
                    n.NodeType,
                    Status = n.Status.ToString(),
                    n.StartedAt,
                    n.CompletedAt,
                    n.Duration
                }),
                execution.PendingApproval
            }),
            errors => Problem(errors));
    }

    [HttpGet("executions/{executionId:guid}/nodes/{nodeId}")]
    public async Task<IActionResult> GetNodeResult(
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var result = await sender.Send(new GetNodeExecutionQuery(executionId, nodeId), ct);

        return result.Match<IActionResult>(
            node => Ok(node),
            errors => Problem(errors));
    }

    // === Approval ===

    [HttpPost("executions/{executionId:guid}/nodes/{nodeId}/approve")]
    public async Task<IActionResult> ApproveNode(
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await sender.Send(new SubmitNodeApprovalCommand(
            executionId,
            nodeId,
            true,
            userId == Guid.Empty ? null : userId), ct);

        return result.Match<IActionResult>(
            _ => Ok(new { approved = true }),
            errors => Problem(errors));
    }

    [HttpPost("executions/{executionId:guid}/nodes/{nodeId}/reject")]
    public async Task<IActionResult> RejectNode(
        Guid executionId,
        string nodeId,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var result = await sender.Send(new SubmitNodeApprovalCommand(
            executionId,
            nodeId,
            false,
            userId == Guid.Empty ? null : userId), ct);

        return result.Match<IActionResult>(
            _ => Ok(new { approved = false }),
            errors => Problem(errors));
    }

    // === Skills (for editor dropdown) ===

    [HttpGet("skills")]
    public IActionResult ListAvailableSkills()
    {
        var skills = skillRegistry.GetAllSkills()
            .Select(s => new
            {
                s.Name,
                s.Description,
                Parameters = s.Parameters
            })
            .OrderBy(s => s.Name)
            .ToList();

        return Ok(skills);
    }
}
