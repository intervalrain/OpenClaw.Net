namespace OpenClaw.Contracts.Channels;

/// <summary>
/// Defines a channel adapter that bridges external messaging platforms
/// (Telegram, Line, Discord, etc.) with the internal Agent pipeline.
/// Implementations should also implement IHostedService to start/stop with the host.
/// </summary>
public interface IChannelAdapter
{
    /// <summary>Unique identifier (e.g. "telegram", "line")</summary>
    string Name { get; }

    /// <summary>Display name (e.g. "Telegram Bot")</summary>
    string DisplayName { get; }

    /// <summary>Current adapter status</summary>
    ChannelAdapterStatus Status { get; }

    /// <summary>
    /// Proactively send a message to an external channel user/group.
    /// Used for notification scenarios (e.g. task completion, cron job results).
    /// </summary>
    /// <param name="externalId">External user/group ID (e.g. Telegram chatId)</param>
    /// <param name="message">Message content</param>
    /// <param name="ct">Cancellation token</param>
    Task SendMessageAsync(string externalId, string message, CancellationToken ct = default);
}
