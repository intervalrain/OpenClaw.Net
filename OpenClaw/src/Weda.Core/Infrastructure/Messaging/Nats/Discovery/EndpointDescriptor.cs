using System.Reflection;
using Weda.Core.Infrastructure.Messaging.Nats.Enums;

namespace Weda.Core.Infrastructure.Messaging.Nats.Discovery;

public class EndpointDescriptor
{
    public required Type ControllerType { get; init; }
    public required MethodInfo Method { get; init; }
    public required string SubjectPattern { get; init; }
    public required EndpointMode Mode { get; init; }
    public required string ConnectionName { get; init; }
    public string? StreamName { get; init; }
    public string? ConsumerName { get; init; }
    public Type? RequestType { get; init; }
    public Type? ResponseType { get; init; }
}