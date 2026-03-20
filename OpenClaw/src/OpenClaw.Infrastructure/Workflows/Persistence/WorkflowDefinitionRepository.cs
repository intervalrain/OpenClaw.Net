using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Workflows;
using OpenClaw.Domain.Workflows.Entities;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Workflows.Persistence;

public class WorkflowDefinitionRepository(AppDbContext context)
    : GenericRepository<WorkflowDefinition, Guid, AppDbContext>(context),
      IWorkflowDefinitionRepository
{
    public async Task<WorkflowDefinition?> GetByIdWithExecutionsAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.Set<WorkflowDefinition>()
            .Include(x => x.Executions.OrderByDescending(e => e.StartedAt).Take(10))
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> GetAllAsync(
        Guid? userId = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        var query = DbContext.Set<WorkflowDefinition>().AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(x => x.CreatedByUserId == userId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        return await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> GetScheduledWorkflowsAsync(CancellationToken ct = default)
    {
        return await DbContext.Set<WorkflowDefinition>()
            .Where(x => x.IsActive && x.ScheduleJson != null)
            .ToListAsync(ct);
    }
}
