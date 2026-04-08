namespace OpenClaw.Contracts.Chat.Requests;

/// <summary>
/// Represents an image attachment in a chat request.
/// </summary>
/// <param name="Base64Data">Base64-encoded image data (without data URI prefix)</param>
/// <param name="MimeType">MIME type of the image (e.g., "image/png", "image/jpeg")</param>
public record ChatImageAttachment(string Base64Data, string MimeType);

public record ChatRequest(
    string Message,
    Guid? ConversationId = null,
    string? Language = null,
    IReadOnlyList<ChatImageAttachment>? Images = null,
    IReadOnlyList<string>? Tools = null,
    IReadOnlyList<string>? Agents = null);