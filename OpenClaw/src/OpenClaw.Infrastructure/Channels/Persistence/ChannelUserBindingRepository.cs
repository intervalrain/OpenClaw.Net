using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Channels.Entities;
using OpenClaw.Domain.Channels.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

namespace OpenClaw.Infrastructure.Channels.Persistence;

public class ChannelUserBindingRepository(AppDbContext context) : IChannelUserBindingRepository
{
    public async Task<ChannelUserBinding?> GetByExternalUserAsync(string platform, string externalUserId, CancellationToken ct = default)
    {
        return await context.Set<ChannelUserBinding>()
            .FirstOrDefaultAsync(x => x.Platform == platform.ToLowerInvariant() && x.ExternalUserId == externalUserId, ct);
    }

    public async Task<List<ChannelUserBinding>> GetByOpenClawUserAsync(Guid openClawUserId, CancellationToken ct = default)
    {
        return await context.Set<ChannelUserBinding>()
            .Where(x => x.OpenClawUserId == openClawUserId)
            .ToListAsync(ct);
    }

    public Task<ChannelUserBinding?> GetByVerificationCodeAsync(string code, CancellationToken ct = default)
    {
        // Verification codes are stored in memory cache, not in DB
        // This is a placeholder for future DB-backed verification
        return Task.FromResult<ChannelUserBinding?>(null);
    }

    public async Task AddAsync(ChannelUserBinding binding, CancellationToken ct = default)
    {
        context.Set<ChannelUserBinding>().Add(binding);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ChannelUserBinding binding, CancellationToken ct = default)
    {
        context.Set<ChannelUserBinding>().Remove(binding);
        await context.SaveChangesAsync(ct);
    }
}
