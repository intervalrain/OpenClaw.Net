using OpenClaw.Domain.Chat.Entities;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Contracts.Llm;

public record ChatMessage(
    ChatRole Role,
    string? Content,
    string? ToolCallId = null,
    IReadOnlyList<ToolCall>? ToolCalls = null);

public static class ChatMessageExtensions
{    
    public static ChatMessage ToLlmMessage(this ConversationMessage message)
        => new(message.Role, message.Content);
}