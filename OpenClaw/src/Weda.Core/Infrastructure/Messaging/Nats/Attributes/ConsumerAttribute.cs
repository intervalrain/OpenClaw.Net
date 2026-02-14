namespace Weda.Core.Infrastructure.Messaging.Nats.Attributes;

/// <summary>
/// Specifies the JetStream consumer name for an EventController
/// Supports patterns: [controller], [action], [method], {version:apiVersion}
/// Example: [Consumer("[controller]_v{version}_consumer)].
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ConsumerAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}