using Microsoft.EntityFrameworkCore;

using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Configuration.Persistence;

public class UserModelProviderRepository(AppDbContext context)
     : GenericRepository<UserModelProvider, Guid, AppDbContext>(context),
       IUserModelProviderRepository
{
    public async Task<List<UserModelProvider>> GetAllByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<UserModelProvider>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<UserModelProvider?> GetByNameAsync(Guid userId, string name, CancellationToken ct = default)
    {
        return await DbContext.Set<UserModelProvider>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Name == name, ct);
    }

    public async Task<UserModelProvider?> GetDefaultAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbContext.Set<UserModelProvider>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.IsDefault, ct);
    }
}
