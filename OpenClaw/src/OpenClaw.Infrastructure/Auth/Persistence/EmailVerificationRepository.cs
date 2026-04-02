using Microsoft.EntityFrameworkCore;

using OpenClaw.Domain.Auth.Entities;
using OpenClaw.Domain.Auth.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Auth.Persistence;

public class EmailVerificationRepository(AppDbContext dbContext)
    : GenericRepository<EmailVerification, Guid, AppDbContext>(dbContext), IEmailVerificationRepository
{
    public async Task<EmailVerification?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<EmailVerification>()
            .Where(e => e.Email == email)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task RemoveByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var records = await DbContext.Set<EmailVerification>()
            .Where(e => e.Email == email)
            .ToListAsync(cancellationToken: cancellationToken);

        DbContext.Set<EmailVerification>().RemoveRange(records);
    }
}