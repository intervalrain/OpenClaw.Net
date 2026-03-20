using System.Text.Json;
using ErrorOr;
using Mediator;
using OpenClaw.Contracts.Workflows;
using OpenClaw.Contracts.Workflows.Commands;
using OpenClaw.Contracts.Workflows.Responses;
using OpenClaw.Domain.Workflows;
using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Workflows.Commands;

public class CloneWorkflowCommandHandler(
    IWorkflowDefinitionRepository repository,
    IUnitOfWork unitOfWork) : IRequestHandler<CloneWorkflowCommand, ErrorOr<WorkflowDefinitionResponse>>
{
    public async ValueTask<ErrorOr<WorkflowDefinitionResponse>> Handle(
        CloneWorkflowCommand request,
        CancellationToken ct)
    {
        var source = await repository.GetByIdAsync(request.SourceWorkflowId, ct);
        if (source is null)
        {
            return Error.NotFound($"Workflow {request.SourceWorkflowId} not found");
        }

        var cloned = source.Clone(request.UserId, request.NewName);

        await repository.AddAsync(cloned, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var definition = JsonSerializer.Deserialize<WorkflowGraph>(
            cloned.DefinitionJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new WorkflowDefinitionResponse
        {
            Id = cloned.Id,
            Name = cloned.Name,
            Description = cloned.Description,
            Definition = definition!,
            Schedule = null,
            CreatedByUserId = cloned.CreatedByUserId,
            CreatedAt = cloned.CreatedAt,
            UpdatedAt = cloned.UpdatedAt,
            IsActive = cloned.IsActive
        };
    }
}
