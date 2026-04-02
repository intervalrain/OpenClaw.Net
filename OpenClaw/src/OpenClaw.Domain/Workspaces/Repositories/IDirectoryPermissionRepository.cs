using OpenClaw.Domain.Workspaces.Entities;

namespace OpenClaw.Domain.Workspaces.Repositories;

public interface IDirectoryPermissionRepository
{
    Task<DirectoryPermission?> GetAsync(Guid ownerUserId, string relativePath, CancellationToken ct = default);
    Task<List<DirectoryPermission>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default);
    Task<List<DirectoryPermission>> GetPublicDirectoriesAsync(CancellationToken ct = default);
    Task AddAsync(DirectoryPermission permission, CancellationToken ct = default);
    Task DeleteAsync(DirectoryPermission permission, CancellationToken ct = default);
}
