using OpenClaw.Domain.Configuration.Entities;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Configuration.Repositories;

public interface IChannelSettingsRepository : IRepository<ChannelSettings, Guid>
{
    Task<ChannelSettings?> GetByChannelTypeAsync(string channelType, CancellationToken cancellationToken = default);
    Task<ChannelSettings?> GetByUserAndChannelTypeAsync(Guid userId, string channelType, CancellationToken cancellationToken = default);
    Task<List<ChannelSettings>> GetAllEnabledByChannelTypeAsync(string channelType, CancellationToken cancellationToken = default);
}
