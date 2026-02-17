using OpenClaw.Domain.Chat.Enums;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Chat.Entities;

public class ConversationMessage(Guid conversationId, ChatRole role, string content) : Entity<Guid>(Guid.NewGuid())
{
    public Guid ConversationId { get; private set; } = conversationId;
    public ChatRole Role { get; private set; } = role;
    public string Content { get; private set; } = content;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private ConversationMessage(): this(Guid.NewGuid(), ChatRole.User, string.Empty) { } // EF Core

    public static ConversationMessage Create(Guid conversationId, ChatRole role, string content)
       => new(conversationId, role, content);
}