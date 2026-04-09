using System.Collections.Concurrent;

using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Channels.Teams.Services;

/// <summary>
/// Maps Teams conversationId to OpenClaw Conversation.
/// Maintains an in-memory cache; DB operations require scoped services passed in by the caller.
/// </summary>
public class TeamsConversationMapper
{
    private readonly ConcurrentDictionary<string, Guid> _conversationMap = new();

    private const string TeamsTitlePrefix = "teams:";

    public async Task<Conversation> GetOrCreateConversationAsync(
        string teamsConversationId,
        string? username,
        Guid? resolvedUserId,
        IConversationRepository repository,
        IUnitOfWork uow,
        CancellationToken ct)
    {
        if (_conversationMap.TryGetValue(teamsConversationId, out var conversationId))
        {
            var existing = await repository.GetByIdAsync(conversationId, ct);
            if (existing is not null)
                return existing;

            _conversationMap.TryRemove(teamsConversationId, out _);
        }

        var title = !string.IsNullOrEmpty(username)
            ? $"{TeamsTitlePrefix}{username}"
            : $"{TeamsTitlePrefix}{teamsConversationId[..Math.Min(20, teamsConversationId.Length)]}";

        var userId = resolvedUserId ?? Guid.Empty;
        var conversation = Conversation.Create(userId, Guid.Empty, title);
        await repository.AddAsync(conversation, ct);
        await uow.SaveChangesAsync(ct);

        _conversationMap[teamsConversationId] = conversation.Id;
        return conversation;
    }

    public void ResetConversation(string teamsConversationId)
    {
        _conversationMap.TryRemove(teamsConversationId, out _);
    }
}
