namespace ClawOS.Contracts.Channels;

public record ChannelMessage(
    string ChannelName,
    string ExternalUserId,
    string? ExternalUserName,
    string Content,
    ChannelMessageType Type = ChannelMessageType.Text);