using OpenClaw.Domain.Workspaces.Entities;
using Weda.Core.Domain;

namespace OpenClaw.Domain.Workspaces.Repositories;

public interface IWorkspaceRepository : IRepository<Workspace, Guid>
{
    Task<Workspace?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default);
    Task<List<Workspace>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Workspace?> GetPersonalAsync(Guid userId, CancellationToken ct = default);
}
