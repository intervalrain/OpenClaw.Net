namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

/// <summary>
/// Options for JetStream consumer error handling (NAK + DLQ)
/// </summary>
public class JetStreamConsumerOptions
{
    /// <summary>
    /// Maximum redelivery attempts before sending to DLQ. Default: 5.
    /// </summary>
    public ulong MaxRedeliveries { get; set; } = 5;

    /// <summary>
    /// NAK delay for transient errors. Default: 5 seconds;
    /// </summary>
    public TimeSpan NakDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether to enable DLQ for failed messages. Default: true.
    /// </summary>
    public bool EnableDlq { get; set; } = true;

    /// <summary>
    /// DLQ stream suffix. DLQ stream name = "{OriginalStream}-dlq". Default: "-dlq".
    /// </summary>
    public string DqlStreamSuffix { get; set; } = "-dlq";
}