using ClawOS.Domain.Chat.Entities;
using ClawOS.Domain.Chat.Enums;

namespace ClawOS.Contracts.Llm;

/// <summary>
/// Represents image content in a chat message.
/// </summary>
/// <param name="Base64Data">Base64-encoded image data (without data URI prefix)</param>
/// <param name="MimeType">MIME type of the image (e.g., "image/png", "image/jpeg")</param>
public record ImageContent(string Base64Data, string MimeType);

public record ChatMessage(
    ChatRole Role,
    string? Content,
    string? ToolCallId = null,
    IReadOnlyList<ToolCall>? ToolCalls = null,
    IReadOnlyList<ImageContent>? Images = null)
{
    /// <summary>
    /// Returns true if this message contains image content.
    /// </summary>
    public bool HasImages => Images is { Count: > 0 };
}

public static class ChatMessageExtensions
{
    public static ChatMessage ToLlmMessage(this ConversationMessage message)
        => new(message.Role, message.Content);
}