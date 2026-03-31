using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Workspaces.Entities;
using OpenClaw.Domain.Workspaces.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Workspaces.Persistence;

public class WorkspaceRepository(AppDbContext context)
    : GenericRepository<Workspace, Guid, AppDbContext>(context), IWorkspaceRepository
{
    public async Task<Workspace?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.Set<Workspace>()
            .Include(w => w.Members)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<List<Workspace>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<Workspace>()
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .Include(w => w.Members)
            .OrderBy(w => w.IsPersonal ? 0 : 1)
            .ThenBy(w => w.Name)
            .ToListAsync(ct);
    }

    public async Task<Workspace?> GetPersonalAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<Workspace>()
            .FirstOrDefaultAsync(w => w.OwnerUserId == userId && w.IsPersonal, ct);
    }
}
