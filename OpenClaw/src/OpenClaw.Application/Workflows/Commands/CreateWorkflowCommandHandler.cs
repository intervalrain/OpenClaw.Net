using System.Text.Json;
using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Contracts.Workflows.Commands;
using OpenClaw.Contracts.Workflows.Responses;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Workflows.Commands;

public class CreateWorkflowCommandHandler(
    IWorkflowDefinitionRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateWorkflowCommand, ErrorOr<WorkflowDefinitionResponse>>
{
    public async ValueTask<ErrorOr<WorkflowDefinitionResponse>> Handle(
        CreateWorkflowCommand request,
        CancellationToken ct)
    {
        // Validate workflow graph
        var validationResult = ValidateWorkflowGraph(request.Definition);
        if (validationResult.IsError)
        {
            return validationResult.Errors;
        }

        var definitionJson = JsonSerializer.Serialize(request.Definition, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var scheduleJson = request.Schedule is not null
            ? JsonSerializer.Serialize(request.Schedule, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
            : null;

        var workflow = WorkflowDefinition.Create(
            request.UserId,
            request.Name,
            request.Description,
            definitionJson,
            scheduleJson);

        await repository.AddAsync(workflow, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ToResponse(workflow, request.Definition, request.Schedule);
    }

    private static ErrorOr<Success> ValidateWorkflowGraph(WorkflowGraph graph)
    {
        // Check for exactly one start node
        var startNodes = graph.Nodes.Count(n => n is StartNode);
        if (startNodes != 1)
        {
            return Error.Validation("Workflow must have exactly one start node");
        }

        // Check for at least one end node
        var endNodes = graph.Nodes.Count(n => n is EndNode);
        if (endNodes < 1)
        {
            return Error.Validation("Workflow must have at least one end node");
        }

        // Check for unique node IDs
        var nodeIds = graph.Nodes.Select(n => n.Id).ToHashSet();
        if (nodeIds.Count != graph.Nodes.Count)
        {
            return Error.Validation("Node IDs must be unique");
        }

        // Check edge references
        foreach (var edge in graph.Edges)
        {
            if (!nodeIds.Contains(edge.Source))
            {
                return Error.Validation($"Edge source '{edge.Source}' does not exist");
            }
            if (!nodeIds.Contains(edge.Target))
            {
                return Error.Validation($"Edge target '{edge.Target}' does not exist");
            }
        }

        return Result.Success;
    }

    private static WorkflowDefinitionResponse ToResponse(
        WorkflowDefinition workflow,
        WorkflowGraph definition,
        ScheduleConfig? schedule) => new()
    {
        Id = workflow.Id,
        Name = workflow.Name,
        Description = workflow.Description,
        Definition = definition,
        Schedule = schedule,
        CreatedByUserId = workflow.CreatedByUserId,
        CreatedAt = workflow.CreatedAt,
        UpdatedAt = workflow.UpdatedAt,
        IsActive = workflow.IsActive
    };
}
