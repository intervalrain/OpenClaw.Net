namespace OpenClaw.Application.AgentActivities;

/// <summary>
/// In-process broadcast channel for real-time agent activity events.
/// SSE controllers subscribe to receive events as they happen.
/// </summary>
public interface IAgentActivityBroadcast
{
    /// <summary>
    /// Publishes an event to all active subscribers.
    /// </summary>
    ValueTask PublishAsync(AgentActivityEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to receive agent activity events. Returns an async enumerable that
    /// yields events as they are published. Each subscriber gets its own channel.
    /// </summary>
    IAsyncEnumerable<AgentActivityEvent> SubscribeAsync(CancellationToken ct = default);
}
