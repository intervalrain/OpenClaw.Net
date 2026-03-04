using OpenClaw.Domain.Configuration.Entities;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Configuration.Repositories;

public interface IChannelSettingsRepository : IRepository<ChannelSettings, Guid>
{
    Task<ChannelSettings?> GetByChannelTypeAsync(string channelType, CancellationToken cancellationToken = default);
}
