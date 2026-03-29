using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Users.Entities;
using OpenClaw.Domain.Users.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Users.Persistence;

public class UserConfigRepository(AppDbContext context)
    : GenericRepository<UserConfig, Guid, AppDbContext>(context), IUserConfigRepository
{
    public async Task<UserConfig?> GetByKeyAsync(Guid userId, string key, CancellationToken ct = default)
    {
        var normalizedKey = key.ToLowerInvariant();
        return await DbContext.Set<UserConfig>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Key == normalizedKey, ct);
    }

    public async Task<IReadOnlyList<UserConfig>> GetAllByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<UserConfig>()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
    }
}
