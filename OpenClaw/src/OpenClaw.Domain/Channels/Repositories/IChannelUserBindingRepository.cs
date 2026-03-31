using OpenClaw.Domain.Channels.Entities;

namespace OpenClaw.Domain.Channels.Repositories;

public interface IChannelUserBindingRepository
{
    Task<ChannelUserBinding?> GetByExternalUserAsync(string platform, string externalUserId, CancellationToken ct = default);
    Task<List<ChannelUserBinding>> GetByOpenClawUserAsync(Guid openClawUserId, CancellationToken ct = default);
    Task<ChannelUserBinding?> GetByVerificationCodeAsync(string code, CancellationToken ct = default);
    Task AddAsync(ChannelUserBinding binding, CancellationToken ct = default);
    Task DeleteAsync(ChannelUserBinding binding, CancellationToken ct = default);
}
