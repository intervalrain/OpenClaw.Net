using Microsoft.EntityFrameworkCore;

using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Configuration.Persistence;

public class AppConfigRepository(AppDbContext context)
     : GenericRepository<AppConfig, Guid, AppDbContext>(context),
       IAppConfigRepository
{
    public async Task<AppConfig?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return await DbContext.Set<AppConfig>()
            .FirstOrDefaultAsync(x => x.Key == key, ct);
    }
}