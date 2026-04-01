using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Workspaces.Entities;
using OpenClaw.Domain.Workspaces.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

namespace OpenClaw.Infrastructure.Workspaces.Persistence;

public class DirectoryPermissionRepository(AppDbContext context) : IDirectoryPermissionRepository
{
    public async Task<DirectoryPermission?> GetAsync(Guid ownerUserId, string relativePath, CancellationToken ct = default)
    {
        var normalized = relativePath.TrimStart('/').TrimEnd('/');
        return await context.Set<DirectoryPermission>()
            .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId && x.RelativePath == normalized, ct);
    }

    public async Task<List<DirectoryPermission>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default)
    {
        return await context.Set<DirectoryPermission>()
            .Where(x => x.OwnerUserId == ownerUserId)
            .OrderBy(x => x.RelativePath)
            .ToListAsync(ct);
    }

    public async Task<List<DirectoryPermission>> GetPublicDirectoriesAsync(CancellationToken ct = default)
    {
        return await context.Set<DirectoryPermission>()
            .Where(x => x.Visibility == DirectoryVisibility.Public || x.Visibility == DirectoryVisibility.PublicReadonly)
            .OrderBy(x => x.OwnerUserId)
            .ThenBy(x => x.RelativePath)
            .ToListAsync(ct);
    }

    public async Task AddAsync(DirectoryPermission permission, CancellationToken ct = default)
    {
        context.Set<DirectoryPermission>().Add(permission);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(DirectoryPermission permission, CancellationToken ct = default)
    {
        context.Set<DirectoryPermission>().Remove(permission);
        await context.SaveChangesAsync(ct);
    }
}
