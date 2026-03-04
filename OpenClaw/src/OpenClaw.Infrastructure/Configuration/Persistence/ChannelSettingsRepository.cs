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
    public async Task<ChannelSettings?> GetByChannelTypeAsync(string channelType, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ChannelSettings>()
            .FirstOrDefaultAsync(x => x.ChannelType == channelType, cancellationToken);
    }
}
