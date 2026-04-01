namespace Weda.Core.Infrastructure.Messaging.Nats.Abstractions;

public interface IPayloadSerializer
{
    /// <summary>
    /// The content type of this serializer (e.g. "application/json", "application/protobuf")
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Deserialize byte array to specific type
    /// </summary>
    object? Deserialize(byte[] data, Type targetType);

    /// <summary>
    /// Serialize object into byte array
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    byte[] Serialize(object value);
}