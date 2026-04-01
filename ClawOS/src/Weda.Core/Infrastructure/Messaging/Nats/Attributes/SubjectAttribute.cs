using Weda.Core.Infrastructure.Messaging.Nats.Enums;

namespace Weda.Core.Infrastructure.Messaging.Nats.Attributes;

/// <summary>
/// Specifies the NATS subject pattern for an EventController method.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class SubjectAttribute(string pattern) : Attribute
{
    public string Pattern { get; } = pattern;
    public DeliveryMode DeliveryMode { get; set; } = DeliveryMode.JetStream;
    public ConsumerMode ConsumerMode { get; set; } = ConsumerMode.Consume;

    /// <summary>
    /// Gets or sets stream. Override class-level stream. Supports pattern.
    /// </summary>
    public string? Stream { get; set; }

    /// <summary>
    /// Gets or sets consumer. Override class-level stream. Supports pattern.
    /// </summary>
    public string? Consumer { get; set; }
}