using ClawOS.Domain.Chat.Entities;

using Weda.Core.Domain;

namespace ClawOS.Domain.Chat.Repositories;

public interface IConversationRepository : IRepository<Conversation, Guid>
{
    Task<List<Conversation>> GetAllByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Conversation?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
}
