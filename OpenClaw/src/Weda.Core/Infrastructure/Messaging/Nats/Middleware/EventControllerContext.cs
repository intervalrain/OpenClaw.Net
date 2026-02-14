using NATS.Client.Core;

using Weda.Core.Infrastructure.Audit;

using Weda.Core.Infrastructure.Messaging.Nats.Discovery;

namespace Weda.Core.Infrastructure.Messaging.Nats.Middleware;

public class EventControllerContext
{
    public required EventController Controller { get; init; }
    public required EndpointDescriptor Endpoint { get; init; }
    public NatsHeaders? Headers { get; init; }
    public required string Subject { get; init; }
    public IAuditContext? AuditContext { get; init; }
    public required IServiceProvider Services { get; init; }
}