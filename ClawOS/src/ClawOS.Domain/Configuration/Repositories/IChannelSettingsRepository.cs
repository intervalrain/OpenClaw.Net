using ClawOS.Domain.Configuration.Entities;

using Weda.Core.Domain;

namespace ClawOS.Domain.Configuration.Repositories;

public interface IChannelSettingsRepository : IRepository<ChannelSettings, Guid>
{
    Task<ChannelSettings?> GetByUserAndChannelTypeAsync(Guid userId, string channelType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all enabled channel settings across ALL users for a given channel type.
    /// Used by background services (e.g. Telegram adapter) to aggregate configs.
    /// Bypasses query filters intentionally — requires system-level context.
    /// </summary>
    Task<List<ChannelSettings>> GetAllEnabledByChannelTypeAsync(string channelType, CancellationToken cancellationToken = default);
}
