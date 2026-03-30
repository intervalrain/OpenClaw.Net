using Microsoft.EntityFrameworkCore;

using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Configuration.Persistence;

public class ChannelSettingsRepository(AppDbContext context)
     : GenericRepository<ChannelSettings, Guid, AppDbContext>(context),
       IChannelSettingsRepository
{
    public async Task<ChannelSettings?> GetByUserAndChannelTypeAsync(Guid userId, string channelType, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ChannelSettings>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ChannelType == channelType, cancellationToken);
    }

    public async Task<List<ChannelSettings>> GetAllEnabledByChannelTypeAsync(string channelType, CancellationToken cancellationToken = default)
    {
        // Explicitly bypass query filters — this is a system-level aggregation
        // used by background services (e.g. Telegram adapter) that need all users' configs.
        return await DbContext.Set<ChannelSettings>()
            .IgnoreQueryFilters()
            .Where(x => x.ChannelType == channelType && x.Enabled)
            .ToListAsync(cancellationToken);
    }
}
