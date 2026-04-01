using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

public interface INatsConnectionProvider
{
    INatsConnection GetConnection(string? name = null);
    NatsJSContext GetJetStreamContext(string? name = null);
}