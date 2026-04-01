namespace Weda.Core.Infrastructure.Messaging.Nats.Enums;

public enum EndpointMode
{
    /// <summary>
    /// Request-Reply pattern using NATS Services.
    /// Supports load balancing and service discovery.
    /// Synchronous communication with response expected.
    /// </summary>
    RequestReply,

    /// <summary>
    /// Publish-Subscribe using Core NATS.
    /// Fire-and-forget, no delivery guarantee.
    /// Asynchronous communication without persistence.
    /// </summary>
    CorePubSub,

    /// <summary>
    /// JetStream continuous consume mode.
    /// Uses consumer.ConsumeAsync() for continuous message processing.
    /// Long-running connection, immediate message processing.
    /// At-least-once delivery with message persistence.
    /// </summary>
    JetStreamConsume,

    /// <summary>
    /// JetStream batch fetch mode.
    /// Uses consumer.FetchAsync() for controlled batch processing.
    /// Short-lived connections, explicit batch size control.
    /// Suitable for scheduled tasks or on-demand processing.
    /// </summary>
    JetStreamFetch,
}