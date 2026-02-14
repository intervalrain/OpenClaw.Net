using NATS.Client.Core;

namespace Weda.Protocol.Serialization;

public class ProtobufSerializerRegistry : INatsSerializerRegistry
{
    public static readonly ProtobufSerializerRegistry Default = new();
    public INatsSerialize<T> GetSerializer<T>() => ProtobufSerializer<T>.Default;
    public INatsDeserialize<T> GetDeserializer<T>() => ProtobufSerializer<T>.Default;

}