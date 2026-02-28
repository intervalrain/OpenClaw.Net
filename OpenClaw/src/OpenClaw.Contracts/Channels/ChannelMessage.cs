namespace OpenClaw.Contracts.Channels;

/// <summary>
/// Unified message format after channel adapter conversion.
/// Each adapter converts external messages (Telegram Update, Line Event, etc.) into this format.
/// </summary>
public record ChannelMessage(
    string ChannelName,
    string ExternalUserId,
    string? ExternalUserName,
    string Content,
    ChannelMessageType Type = ChannelMessageType.Text);

public enum ChannelMessageType
{
    Text,
    Image,
    File,
    Command
}
