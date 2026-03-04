namespace OpenClaw.Contracts.Channels;

/// <summary>
/// Event published to JetStream when a channel message is received.
/// Platform-agnostic - works for Telegram, Line, Discord, etc.
/// </summary>
public record ChannelMessageReceivedEvent(
    string ChannelName,
    string ExternalChatId,
    string ExternalUserId,
    string? ExternalUsername,
    string? Content,
    string? ExternalMessageId,
    ChannelMessageType Type,
    DateTimeOffset ReceivedAt);