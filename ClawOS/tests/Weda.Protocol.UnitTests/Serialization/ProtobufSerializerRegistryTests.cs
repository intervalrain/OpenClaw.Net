using Shouldly;
using Weda.Protocol.Serialization;

namespace Weda.Protocol.UnitTests.Serialization;

public class ProtobufSerializerRegistryTests
{
    [Fact]
    public void Default_IsSingleton()
    {
        var registry1 = ProtobufSerializerRegistry.Default;
        var registry2 = ProtobufSerializerRegistry.Default;

        registry1.ShouldBeSameAs(registry2);
    }

    [Fact]
    public void GetSerializer_ReturnsProtobufSerializer()
    {
        var registry = ProtobufSerializerRegistry.Default;
        var serializer = registry.GetSerializer<TestMessage>();

        serializer.ShouldNotBeNull();
        serializer.ShouldBeOfType<ProtobufSerializer<TestMessage>>();
    }

    [Fact]
    public void GetDeserializer_ReturnsProtobufSerializer()
    {
        var registry = ProtobufSerializerRegistry.Default;
        var deserializer = registry.GetDeserializer<TestMessage>();

        deserializer.ShouldNotBeNull();
        deserializer.ShouldBeOfType<ProtobufSerializer<TestMessage>>();
    }

    [Fact]
    public void GetSerializer_ReturnsSameInstance()
    {
        var registry = ProtobufSerializerRegistry.Default;
        var serializer1 = registry.GetSerializer<TestMessage>();
        var serializer2 = registry.GetSerializer<TestMessage>();

        serializer1.ShouldBeSameAs(serializer2);
    }
}
