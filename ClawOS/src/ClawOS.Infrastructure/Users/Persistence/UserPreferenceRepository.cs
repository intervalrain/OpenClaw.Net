using Microsoft.EntityFrameworkCore;

using ClawOS.Domain.Users.Entities;
using ClawOS.Domain.Users.Repositories;
using ClawOS.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace ClawOS.Infrastructure.Users.Persistence;

public class UserPreferenceRepository(AppDbContext context)
    : GenericRepository<UserPreference, Guid, AppDbContext>(context),
      IUserPreferenceRepository
{
    public async Task<UserPreference?> GetByKeyAsync(Guid userId, string key, CancellationToken ct = default)
    {
        var normalizedKey = key.ToLowerInvariant();
        return await DbContext.Set<UserPreference>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Key == normalizedKey, ct);
    }

    public async Task<List<UserPreference>> GetAllByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<UserPreference>()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
    }

    public async Task<List<UserPreference>> GetByPrefixAsync(Guid userId, string keyPrefix, CancellationToken ct = default)
    {
        var normalizedPrefix = keyPrefix.ToLowerInvariant();
        return await DbContext.Set<UserPreference>()
            .Where(x => x.UserId == userId && x.Key.StartsWith(normalizedPrefix))
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
    }

    public async Task DeleteByKeyAsync(Guid userId, string key, CancellationToken ct = default)
    {
        var normalizedKey = key.ToLowerInvariant();
        var preference = await DbContext.Set<UserPreference>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Key == normalizedKey, ct);

        if (preference != null)
        {
            DbContext.Set<UserPreference>().Remove(preference);
        }
    }
}
