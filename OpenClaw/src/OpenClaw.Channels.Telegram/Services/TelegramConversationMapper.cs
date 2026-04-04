using System.Collections.Concurrent;

using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Channels.Telegram.Services;

/// <summary>
/// Maps Telegram chatId to OpenClaw Conversation.
/// Maintains an in-memory cache; DB operations require scoped services passed in by the caller.
/// </summary>
public class TelegramConversationMapper
{
    private readonly ConcurrentDictionary<long, Guid> _chatConversationMap = new();

    private const string TelegramTitlePrefix = "tg:";

    public async Task<Conversation> GetOrCreateConversationAsync(
        long chatId,
        string? username,
        Guid? resolvedUserId,
        IConversationRepository repository,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        // Check cache first
        if (_chatConversationMap.TryGetValue(chatId, out var conversationId))
        {
            var existing = await repository.GetByIdAsync(conversationId, ct);
            if (existing is not null)
                return existing;

            _chatConversationMap.TryRemove(chatId, out _);
        }

        // Create new conversation with resolved user (or Guid.Empty if unbound)
        var title = !string.IsNullOrEmpty(username)
            ? $"{TelegramTitlePrefix}{username}"
            : $"{TelegramTitlePrefix}{chatId}";

        var userId = resolvedUserId ?? Guid.Empty;
        var conversation = Conversation.Create(userId, Guid.Empty, title);
        await repository.AddAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);

        _chatConversationMap[chatId] = conversation.Id;
        return conversation;
    }

    public void ResetConversation(long chatId)
    {
        _chatConversationMap.TryRemove(chatId, out _);
    }
}
