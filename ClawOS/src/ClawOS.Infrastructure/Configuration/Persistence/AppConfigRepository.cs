using Microsoft.EntityFrameworkCore;

using ClawOS.Domain.Configuration.Entities;
using ClawOS.Domain.Configuration.Repositories;
using ClawOS.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace ClawOS.Infrastructure.Configuration.Persistence;

public class AppConfigRepository(AppDbContext context)
     : GenericRepository<AppConfig, Guid, AppDbContext>(context),
       IAppConfigRepository
{
    public async Task<AppConfig?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        // Use case-insensitive comparison for config keys
        return await DbContext.Set<AppConfig>()
            .FirstOrDefaultAsync(x => x.Key.ToLower() == key.ToLower(), ct);
    }
}