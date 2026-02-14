using System.Buffers;
using System.Reflection;

using Google.Protobuf;

using NATS.Client.Core;

namespace Weda.Protocol.Serialization;

public class ProtobufSerializer<T> : INatsSerializer<T>
{
    public static readonly ProtobufSerializer<T> Default = new();

    public void Serialize(IBufferWriter<byte> bufferWriter, T value)
    {
        if (value is IMessage message)
        {
            message.WriteTo(bufferWriter);
        }
        else
        {
            throw new InvalidOperationException($"Cannot serialize {typeof(T).Name}. Type must implement IMessage.");
        }
    }

    public T? Deserialize(in ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsEmpty)
            return default;

        var parser = typeof(T).GetProperty("Parser", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as MessageParser
            ?? throw new InvalidOperationException($"Cannot find parser for {typeof(T).Name}. Type must be a protobuf message.");
        
        return (T)(object)parser.ParseFrom(buffer);
    }

    public INatsSerializer<T> CombineWith(INatsSerializer<T> next)
    {
        return typeof(IMessage).IsAssignableFrom(typeof(T)) ? this : next;
    }
}