using OpenClaw.Domain.Chat.Enums;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Chat.Entities;

public class Conversation(string title, Guid userId) : AggregateRoot<Guid>(Guid.NewGuid()), IUserScoped
{
    public string? Title { get; private set; } = title;
    public Guid UserId { get; private set; } = userId;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    public List<ConversationMessage> Messages { get; private set; } = [];

    private Conversation(): this(string.Empty, Guid.Empty) { }

    public static Conversation Create(Guid userId, string? title = null)
        => new(title ?? "New Chat", userId);

    public void AddMessage(ChatRole role, string content)
    {
        Messages.Add(ConversationMessage.Create(Id, role, content));
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTitle(string title)
    {
        if (string.Equals(Title, title, StringComparison.OrdinalIgnoreCase)) return;
        Title = title;
        UpdatedAt = DateTime.UtcNow;
    }

    public Guid GetOwnerUserId() => UserId;
}