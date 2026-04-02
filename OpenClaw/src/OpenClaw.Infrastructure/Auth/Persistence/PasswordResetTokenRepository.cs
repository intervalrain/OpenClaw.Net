using Microsoft.EntityFrameworkCore;
using OpenClaw.Domain.Auth.Entities;
using OpenClaw.Domain.Auth.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;
using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Auth.Persistence;

public class PasswordResetTokenRepository(AppDbContext dbContext)
    : GenericRepository<PasswordResetToken, Guid, AppDbContext>(dbContext), IPasswordResetTokenRepository
{
    public async Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await DbContext.Set<PasswordResetToken>()
            .FirstOrDefaultAsync(t => t.Token == token, ct);
    }

    public async Task InvalidateAllForEmailAsync(string email, CancellationToken ct = default)
    {
        var tokens = await DbContext.Set<PasswordResetToken>()
            .Where(t => t.Email == email && !t.IsUsed)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.MarkUsed();
    }
}
