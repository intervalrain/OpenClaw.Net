using System.Collections.Concurrent;
using NATS.Client.Core;
using NATS.Client.JetStream;

namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

public class NatsConnectionProvider(
    Dictionary<string, NatsOpts> configs,
    string defaultConnection) : INatsConnectionProvider, IAsyncDisposable
{
    private readonly Dictionary<string, NatsOpts> _configs = configs;
    private readonly string _defaultConnection = defaultConnection;
    private readonly ConcurrentDictionary<string, INatsConnection> _connections = [];
    private readonly ConcurrentDictionary<string, NatsJSContext> _jsContexts = [];

    public INatsConnection GetConnection(string? name = null)
    {
        var connectionName = name ?? _defaultConnection;

        return _connections.GetOrAdd(connectionName, key =>
        {
            if (!_configs.TryGetValue(key, out var opts))
            {
                throw new InvalidOperationException($"NATS connection '{key}' not configured.");
            }

            return new NatsConnection(opts);
        });
    }

    public NatsJSContext GetJetStreamContext(string? name = null)
    {
        var connectionName = name ?? _defaultConnection;

        return _jsContexts.GetOrAdd(connectionName, key =>
        {
            var connection = GetConnection(key);
            return new NatsJSContext(connection);
        });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            await connection.DisposeAsync();
        }

        _connections.Clear();
        _jsContexts.Clear();
    }
}
