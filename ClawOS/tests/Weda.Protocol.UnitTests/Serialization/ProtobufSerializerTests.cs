using System.Buffers;
using Shouldly;
using Weda.Protocol.Serialization;

namespace Weda.Protocol.UnitTests.Serialization;

public class ProtobufSerializerTests
{
    private readonly ProtobufSerializer<TestMessage> _serializer = ProtobufSerializer<TestMessage>.Default;

    [Fact]
    public void Serialize_ValidMessage_WritesToBuffer()
    {
        var message = new TestMessage { Name = "Test", Value = 42 };
        var buffer = new ArrayBufferWriter<byte>();

        _serializer.Serialize(buffer, message);

        buffer.WrittenCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Deserialize_ValidBuffer_ReturnsMessage()
    {
        var original = new TestMessage { Name = "Hello", Value = 123 };
        var buffer = new ArrayBufferWriter<byte>();
        _serializer.Serialize(buffer, original);

        var sequence = new ReadOnlySequence<byte>(buffer.WrittenMemory);
        var deserialized = _serializer.Deserialize(sequence);

        deserialized.ShouldNotBeNull();
        deserialized!.Name.ShouldBe("Hello");
        deserialized.Value.ShouldBe(123);
    }

    [Fact]
    public void Deserialize_EmptyBuffer_ReturnsDefault()
    {
        var sequence = new ReadOnlySequence<byte>(Array.Empty<byte>());
        var result = _serializer.Deserialize(sequence);

        result.ShouldBeNull();
    }

    [Fact]
    public void Serialize_NonIMessage_ThrowsException()
    {
        var serializer = ProtobufSerializer<string>.Default;
        var buffer = new ArrayBufferWriter<byte>();

        var ex = Should.Throw<InvalidOperationException>(() => serializer.Serialize(buffer, "test"));
        ex.Message.ShouldContain("Cannot serialize");
    }

    [Fact]
    public void Roundtrip_ComplexMessage_PreservesData()
    {
        var original = new TestMessage { Name = "Complex Test 中文", Value = int.MaxValue };
        var buffer = new ArrayBufferWriter<byte>();

        _serializer.Serialize(buffer, original);
        var sequence = new ReadOnlySequence<byte>(buffer.WrittenMemory);
        var deserialized = _serializer.Deserialize(sequence);

        deserialized.ShouldNotBeNull();
        deserialized!.Name.ShouldBe(original.Name);
        deserialized.Value.ShouldBe(original.Value);
    }

    [Fact]
    public void Roundtrip_DefaultValues_ReturnsNull()
    {
        // Protobuf doesn't write default values, so empty message serializes to 0 bytes
        var original = new TestMessage { Name = "", Value = 0 };
        var buffer = new ArrayBufferWriter<byte>();

        _serializer.Serialize(buffer, original);

        // Empty message serializes to empty buffer
        buffer.WrittenCount.ShouldBe(0);

        var sequence = new ReadOnlySequence<byte>(buffer.WrittenMemory);
        var deserialized = _serializer.Deserialize(sequence);

        // Empty buffer returns null (matches Deserialize_EmptyBuffer_ReturnsDefault behavior)
        deserialized.ShouldBeNull();
    }

    [Fact]
    public void CombineWith_IMessageType_ReturnsThis()
    {
        var other = new ProtobufSerializer<TestMessage>();
        var result = _serializer.CombineWith(other);

        result.ShouldBeSameAs(_serializer);
    }

    [Fact]
    public void CombineWith_NonIMessageType_ReturnsNext()
    {
        var stringSerializer = ProtobufSerializer<string>.Default;
        var next = ProtobufSerializer<string>.Default;

        var result = stringSerializer.CombineWith(next);

        result.ShouldBeSameAs(next);
    }
}
