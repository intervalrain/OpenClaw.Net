namespace Weda.Core.Infrastructure.Messaging.Nats.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ConnectionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}