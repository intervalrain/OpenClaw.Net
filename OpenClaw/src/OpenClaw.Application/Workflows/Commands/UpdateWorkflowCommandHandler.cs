using System.Text.Json;
using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Contracts.Workflows.Commands;
using OpenClaw.Contracts.Workflows.Responses;
using OpenClaw.Domain.Workflows;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Workflows.Commands;

public class UpdateWorkflowCommandHandler(
    IWorkflowDefinitionRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateWorkflowCommand, ErrorOr<WorkflowDefinitionResponse>>
{
    public async ValueTask<ErrorOr<WorkflowDefinitionResponse>> Handle(
        UpdateWorkflowCommand request,
        CancellationToken ct)
    {
        var workflow = await repository.GetByIdAsync(request.WorkflowId, ct);
        if (workflow is null)
        {
            return Error.NotFound($"Workflow {request.WorkflowId} not found");
        }

        string? definitionJson = null;
        if (request.Definition is not null)
        {
            definitionJson = JsonSerializer.Serialize(request.Definition, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        string? scheduleJson = null;
        if (request.Schedule is not null)
        {
            scheduleJson = JsonSerializer.Serialize(request.Schedule, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }

        workflow.Update(
            request.Name,
            request.Description,
            definitionJson,
            scheduleJson,
            request.IsActive);

        await repository.UpdateAsync(workflow, ct);
        await unitOfWork.SaveChangesAsync(ct);

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
