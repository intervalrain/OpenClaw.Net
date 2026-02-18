namespace OpenClaw.Contracts.Chat.Requests;

public record ChatRequest(string Message, Guid? ConversationId = null);