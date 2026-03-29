using OpenClaw.Domain.Chat.Entities;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Chat.Repositories;

public interface IConversationRepository : IRepository<Conversation, Guid>
{
    Task<List<Conversation>> GetAllByUserAsync(Guid userId, CancellationToken ct = default);
    Task<Conversation?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);
}
