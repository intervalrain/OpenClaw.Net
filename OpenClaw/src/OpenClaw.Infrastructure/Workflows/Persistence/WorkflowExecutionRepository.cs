using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Workflows.Persistence;

public class WorkflowExecutionRepository(AppDbContext context)
    : GenericRepository<WorkflowExecution, Guid, AppDbContext>(context),
      IWorkflowExecutionRepository
{
    public async Task<WorkflowExecution?> GetByIdWithNodesAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.Set<WorkflowExecution>()
            .Include(x => x.NodeExecutions)
            .Include(x => x.WorkflowDefinition)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<WorkflowExecution>> GetByWorkflowIdAsync(
        Guid workflowId,
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return await DbContext.Set<WorkflowExecution>()
            .Where(x => x.WorkflowDefinitionId == workflowId)
            .OrderByDescending(x => x.StartedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowExecution>> GetRecentAsync(
        int limit = 20,
        int offset = 0,
        CancellationToken ct = default)
    {
        return await DbContext.Set<WorkflowExecution>()
            .Include(x => x.WorkflowDefinition)
            .OrderByDescending(x => x.StartedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }
}
