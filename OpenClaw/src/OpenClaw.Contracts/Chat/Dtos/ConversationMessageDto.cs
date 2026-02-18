using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Contracts.Chat.Dtos;

public record ConversationMessageDto(ChatRole Role, string Content);