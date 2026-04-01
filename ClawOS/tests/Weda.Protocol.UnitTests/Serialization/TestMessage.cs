using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Weda.Protocol.UnitTests.Serialization;

/// <summary>
/// A simple test message implementing IMessage for unit testing.
/// </summary>
public class TestMessage : IMessage<TestMessage>, IBufferMessage
{
    public static MessageParser<TestMessage> Parser { get; } = new(() => new TestMessage());

    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }

    public MessageDescriptor Descriptor => null!;

    public void MergeFrom(TestMessage message)
    {
        Name = message.Name;
        Value = message.Value;
    }

    public void MergeFrom(CodedInputStream input)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 10: // field 1, wire type 2 (length-delimited)
                    Name = input.ReadString();
                    break;
                case 16: // field 2, wire type 0 (varint)
                    Value = input.ReadInt32();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
    }

    public void WriteTo(CodedOutputStream output)
    {
        if (!string.IsNullOrEmpty(Name))
        {
            output.WriteRawTag(10); // field 1, wire type 2
            output.WriteString(Name);
        }
        if (Value != 0)
        {
            output.WriteRawTag(16); // field 2, wire type 0
            output.WriteInt32(Value);
        }
    }

    public int CalculateSize()
    {
        int size = 0;
        if (!string.IsNullOrEmpty(Name))
        {
            size += 1 + CodedOutputStream.ComputeStringSize(Name);
        }
        if (Value != 0)
        {
            size += 1 + CodedOutputStream.ComputeInt32Size(Value);
        }
        return size;
    }

    public TestMessage Clone() => new() { Name = Name, Value = Value };

    public bool Equals(TestMessage? other)
    {
        if (other is null) return false;
        return Name == other.Name && Value == other.Value;
    }

    public override bool Equals(object? obj) => Equals(obj as TestMessage);
    public override int GetHashCode() => HashCode.Combine(Name, Value);

    public void InternalMergeFrom(ref ParseContext input)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 10:
                    Name = input.ReadString();
                    break;
                case 16:
                    Value = input.ReadInt32();
                    break;
            }
        }
    }

    public void InternalWriteTo(ref WriteContext output)
    {
        if (!string.IsNullOrEmpty(Name))
        {
            output.WriteRawTag(10);
            output.WriteString(Name);
        }
        if (Value != 0)
        {
            output.WriteRawTag(16);
            output.WriteInt32(Value);
        }
    }
}