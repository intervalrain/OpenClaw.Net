using OpenClaw.Domain.Workflows.Entities;

namespace OpenClaw.Domain.Workflows;

public interface IWorkflowDefinitionRepository
{
    Task<WorkflowDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetByIdWithExecutionsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(Guid? userId = null, bool? isActive = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowDefinition>> GetScheduledWorkflowsAsync(CancellationToken ct = default);
    Task AddAsync(WorkflowDefinition workflow, CancellationToken ct = default);
    Task UpdateAsync(WorkflowDefinition workflow, CancellationToken ct = default);
    Task DeleteAsync(WorkflowDefinition workflow, CancellationToken ct = default);
}
