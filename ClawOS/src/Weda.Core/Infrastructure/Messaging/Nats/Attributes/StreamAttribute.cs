namespace Weda.Core.Infrastructure.Messaging.Nats.Attributes;

/// <summary>
/// Specifies the stream name for EventController
/// Supports patterns: [controller], [action], [method], {version:apiVersion}
/// Example: [Stream("[controller]_v{version}_stream")].
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class StreamAttribute(string template) : Attribute
{
    public string Template { get; } = template;
}