using OpenClaw.Domain.Workflows.Entities;

namespace OpenClaw.Domain.Workflows;

public interface IWorkflowExecutionRepository
{
    Task<WorkflowExecution?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowExecution?> GetByIdWithNodesAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowExecution>> GetByWorkflowIdAsync(Guid workflowId, int limit = 20, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowExecution>> GetRecentAsync(int limit = 20, int offset = 0, CancellationToken ct = default);
    Task AddAsync(WorkflowExecution execution, CancellationToken ct = default);
    Task UpdateAsync(WorkflowExecution execution, CancellationToken ct = default);
}
