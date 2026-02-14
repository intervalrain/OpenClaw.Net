using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using NATS.Net;
using Weda.Core.Infrastructure.Messaging.Nats.Abstractions;
using Weda.Core.Infrastructure.Messaging.Nats.Configuration;
using Weda.Protocol.Enums;
using Weda.Protocol.Serialization;

namespace Weda.Protocol;

public static class NatsBuilderExtensions
{
    public static NatsBuilder UseWedaProtocol(this NatsBuilder builder)
    {
        builder.Services.AddSingleton<ISubjectResolver, WedaSubjectResolver>();
        return builder;
    }

    /// <summary>
    /// Configures a connection to use Protobuf serialization
    /// </summary>
    public static NatsBuilder UseProtobuf(this NatsBuilder builder, string connectionName)
    {
        return builder.ConfigureConnection(connectionName, opts =>
            opts with { SerializerRegistry = ProtobufSerializerRegistry.Default });
    }

    /// <summary>
    /// Configures the default connection to use Protobuf serialization
    /// </summary>
    public static NatsBuilder UseProtobuf(this NatsBuilder builder)
    {
        return builder.UseProtobuf(builder.DefaultConnection);
    }

    public static NatsBuilder AddConnection(
        this NatsBuilder builder,
        EcoType protocol,
        string name,
        string url)
    {
        INatsSerializerRegistry registry = protocol switch
        {
            EcoType.Json => NatsClientDefaultSerializerRegistry.Default,
            EcoType.Protobuf => ProtobufSerializerRegistry.Default,
            _ => throw new ArgumentOutOfRangeException(nameof(protocol))
        };

        var opts = NatsOpts.Default with
        {
            Url = url,
            SerializerRegistry = registry
        };

        return builder.AddConnection(name, opts);
    }
}