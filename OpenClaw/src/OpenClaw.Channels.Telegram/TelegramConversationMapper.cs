using System.Collections.Concurrent;

using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Channels.Telegram;

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

            // Cached conversation was deleted, remove stale cache
            _chatConversationMap.TryRemove(chatId, out _);
        }

        // Create new conversation
        var title = !string.IsNullOrEmpty(username)
            ? $"{TelegramTitlePrefix}{username}"
            : $"{TelegramTitlePrefix}{chatId}";

        var conversation = Conversation.Create(title);
        await repository.AddAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);

        _chatConversationMap[chatId] = conversation.Id;
        return conversation;
    }

    /// <summary>
    /// Resets the conversation for a given chatId (used by /new command).
    /// Next call to GetOrCreateConversationAsync will create a new conversation.
    /// </summary>
    public void ResetConversation(long chatId)
    {
        _chatConversationMap.TryRemove(chatId, out _);
    }
}
