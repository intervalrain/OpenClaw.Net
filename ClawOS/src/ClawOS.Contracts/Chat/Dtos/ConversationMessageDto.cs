using ClawOS.Domain.Chat.Enums;

namespace ClawOS.Contracts.Chat.Dtos;

public record ConversationMessageDto(ChatRole Role, string Content);