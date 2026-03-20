using System.Text.Json;
using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Contracts.Workflows.Queries;
using OpenClaw.Contracts.Workflows.Responses;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;

namespace OpenClaw.Application.Workflows.Queries;

public class GetWorkflowsQueryHandler(
    IWorkflowDefinitionRepository repository) : IRequestHandler<GetWorkflowsQuery, ErrorOr<IReadOnlyList<WorkflowSummaryResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<WorkflowSummaryResponse>>> Handle(
        GetWorkflowsQuery request,
        CancellationToken ct)
    {
        var workflows = await repository.GetAllAsync(request.UserId, request.IsActive, ct);

        var responses = workflows.Select(ToSummaryResponse).ToList();
        return responses;
    }

    private static WorkflowSummaryResponse ToSummaryResponse(WorkflowDefinition workflow)
    {
        var definition = JsonSerializer.Deserialize<WorkflowGraph>(
            workflow.DefinitionJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        ScheduleConfig? schedule = null;
        if (workflow.ScheduleJson is not null)
        {
            schedule = JsonSerializer.Deserialize<ScheduleConfig>(
                workflow.ScheduleJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var lastExecution = workflow.Executions
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefault();

        return new WorkflowSummaryResponse
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Schedule = schedule,
            NodeCount = definition?.Nodes.Count ?? 0,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            IsActive = workflow.IsActive,
            LastExecution = lastExecution is not null ? new WorkflowExecutionSummary
            {
                Id = lastExecution.Id,
                Status = lastExecution.Status,
                StartedAt = lastExecution.StartedAt,
                CompletedAt = lastExecution.CompletedAt
            } : null
        };
    }
}

public class GetWorkflowQueryHandler(
    IWorkflowDefinitionRepository repository) : IRequestHandler<GetWorkflowQuery, ErrorOr<WorkflowDefinitionResponse>>
{
    public async ValueTask<ErrorOr<WorkflowDefinitionResponse>> Handle(
        GetWorkflowQuery request,
        CancellationToken ct)
    {
        var workflow = await repository.GetByIdAsync(request.WorkflowId, ct);
        if (workflow is null)
        {
            return Error.NotFound($"Workflow {request.WorkflowId} not found");
        }

        var definition = JsonSerializer.Deserialize<WorkflowGraph>(
            workflow.DefinitionJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        ScheduleConfig? schedule = null;
        if (workflow.ScheduleJson is not null)
        {
            schedule = JsonSerializer.Deserialize<ScheduleConfig>(
                workflow.ScheduleJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        return new WorkflowDefinitionResponse
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            Definition = definition!,
            Schedule = schedule,
            CreatedByUserId = workflow.CreatedByUserId,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            IsActive = workflow.IsActive
        };
    }
}
