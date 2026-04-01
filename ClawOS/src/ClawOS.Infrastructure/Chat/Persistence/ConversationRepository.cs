using Microsoft.EntityFrameworkCore;

using ClawOS.Domain.Chat.Entities;
using ClawOS.Domain.Chat.Repositories;
using ClawOS.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace ClawOS.Infrastructure.Chat.Persistence;

public class ConversationRepository(AppDbContext dbContext)
    : GenericRepository<Conversation, Guid, AppDbContext>(dbContext), IConversationRepository
{
    public override async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Conversation?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
    }

    public override async Task<List<Conversation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Messages)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Conversation>> GetAllByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(c => c.Messages)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync(ct);
    }
}
