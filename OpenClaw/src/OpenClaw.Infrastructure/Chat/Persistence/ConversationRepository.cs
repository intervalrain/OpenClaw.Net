using Microsoft.EntityFrameworkCore;

using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Chat.Persistence;

public class ConversationRepository(AppDbContext dbContext)
    : GenericRepository<Conversation, Guid, AppDbContext>(dbContext), IConversationRepository
{
    public override async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public override async Task<List<Conversation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Messages)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}