using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NATS.Client.Core;
using NATS.Net;
using Weda.Core.Application.Interfaces.Messaging;
using Weda.Core.Application.Interfaces.Storage;
using Weda.Core.Infrastructure.Messaging.Nats.Caching;
using Weda.Core.Infrastructure.Messaging.Nats.ObjectStore;

namespace Weda.Core.Infrastructure.Messaging.Nats.Configuration;

public class NatsBuilder(IServiceCollection services)
{
    private readonly Dictionary<string, NatsOpts> _connections = [];
    private readonly Dictionary<string, Func<NatsOpts, NatsOpts>> _connectionConfigurators = [];

    public IServiceCollection Services { get; } = services;
    public string DefaultConnection { get; set; } = "default";

    public NatsBuilder AddConnection(string name, string url, Func<NatsOpts, NatsOpts>? configure = null)
    {
        var opts = NatsOpts.Default with
        {
            Url = url,
            SerializerRegistry = NatsClientDefaultSerializerRegistry.Default
        };

        if (configure is not null)
        {
            opts = configure(opts);
        }

        _connections[name] = opts;
        return this;
    }

    public NatsBuilder AddConnection(string name, NatsOpts opts)
    {
        _connections[name] = opts;
        return this;
    }

    public NatsBuilder AddKeyValueCache(Action<NatsKvCacheOptions>? configure = null)
    {
        Services.AddNatsKvCache(configure);
        return this;
    }

    public NatsBuilder AddObjectStore(Action<NatsObjectStoreOptions>? configure = null)
    {
        Services.AddNatsObjectStore(configure);
        return this;
    }

    public NatsBuilder ConfigureResilience(Action<JetStreamResilienceOptions>? configure = null)
    {
        var options = new JetStreamResilienceOptions();
        configure?.Invoke(options);

        Services.Configure<JetStreamResilienceOptions>(opt =>
        {
            opt.MaxRetryAttempts = options.MaxRetryAttempts;
            opt.BaseDelay = options.BaseDelay;
            opt.FailureRatio = options.FailureRatio;
            opt.SamplingDuration = options.SamplingDuration;
            opt.BreakDuration = options.BreakDuration;
            opt.MinimumThroughput = options.MinimumThroughput;
        });

        return this;
    }

    /// <summary>
    /// Binds configuration from NatsOptions section
    /// </summary>
    public NatsBuilder BindConfiguration(NatsOptions options)
    {
        DefaultConnection = options.DefaultConnection;

        foreach (var (name, config) in options.Connections)
        {
            var opts = CreateNatsOpts(config);
            _connections[name] = opts;
        }

        return this;
    }

    public NatsBuilder ConfigureConnection(string name, Func<NatsOpts, NatsOpts> configure)
    {
        _connectionConfigurators[name] = configure;
        return this;
    }

    internal void Build()
    {
        foreach (var (name, configurator) in _connectionConfigurators)
        {
            if (_connections.TryGetValue(name, out var opts))
            {
                _connections[name] = configurator(opts);
            }
        }

        Services.TryAddSingleton<INatsConnectionProvider>(sp => new NatsConnectionProvider(_connections, DefaultConnection));
        Services.TryAddSingleton(sp => sp.GetRequiredService<INatsConnectionProvider>().GetConnection());
        Services.TryAddSingleton(sp => sp.GetRequiredService<INatsConnectionProvider>().GetJetStreamContext());
        Services.TryAddSingleton<IJetStreamClientFactory, JetStreamClientFactory>();
    }

    private static NatsOpts CreateNatsOpts(NatsConnectionConfig config)
    {
        var opts = NatsOpts.Default with
        {
            Url = config.Url,
            Name = config.Name,
            SerializerRegistry = NatsClientDefaultSerializerRegistry.Default
        };

        opts = opts with
        {
            AuthOpts = NatsAuthOpts.Default with
            {
                Username = config.Username,
                Password = config.Password,
                Token = config.Token,
                Jwt = config.Jwt,
                NKey = config.NKey,
                Seed = config.Seed,
                CredsFile = config.CredsFile,
                NKeyFile = config.NKeyFile
            }
        };

        return opts;
    }
}