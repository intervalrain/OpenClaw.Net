using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Chat.Persistence;

public class ConversationRepository(AppDbContext dbContext)
    : GenericRepository<Conversation, Guid, AppDbContext>(dbContext), IConversationRepository;