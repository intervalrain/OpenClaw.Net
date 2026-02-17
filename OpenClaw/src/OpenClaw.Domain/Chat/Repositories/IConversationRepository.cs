using OpenClaw.Domain.Chat.Entities;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Chat.Repositories;

public interface IConversationRepository : IRepository<Conversation, Guid>;