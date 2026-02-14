namespace Weda.Core.Infrastructure.Messaging.Nats.Enums;

public enum ConsumerMode
{
    /// <summary>
    /// Continuous consume mode using ConsumeAsync().
    /// Long-running connection for immediate message processing.
    /// Default mode for event-driven scenarios.
    /// </summary>
    Consume,

    /// <summary>
    /// Batch fetch mode using FetchAsync().
    /// Explicit batch size control for scheduled or on-demand processing.
    /// Suitable for tasks that don't need continuous listening.
    /// </summary>
    Fetch,
}