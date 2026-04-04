using OpenClaw.Application.HierarchicalAgents;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class TaskGraphSerializerTests
{
    [Fact]
    public void DeserializeJson_ValidJson_ReturnsGraph()
    {
        var json = """
        {
            "name": "video-pipeline",
            "nodes": [
                { "id": "script", "agent": "script-writer", "input": { "topic": "AI" } },
                { "id": "voice", "agent": "tts" },
                { "id": "compose", "agent": "video-composer" }
            ],
            "edges": [
                { "from": "script", "to": "voice", "mapping": "$.output.text" },
                { "from": "voice", "to": "compose", "mapping": "$.output.audioPath" }
            ]
        }
        """;

        var graph = TaskGraphSerializer.DeserializeJson(json);

        graph.ShouldNotBeNull();
        graph!.Name.ShouldBe("video-pipeline");
        graph.Nodes.Count.ShouldBe(3);
        graph.Edges.Count.ShouldBe(2);

        graph.Nodes[0].Id.ShouldBe("script");
        graph.Nodes[0].AgentName.ShouldBe("script-writer");
        graph.Nodes[0].Input.ShouldNotBeNull();
        graph.Nodes[0].Input!.RootElement.GetProperty("topic").GetString().ShouldBe("AI");

        graph.Edges[0].FromNodeId.ShouldBe("script");
        graph.Edges[0].ToNodeId.ShouldBe("voice");
        graph.Edges[0].OutputMapping.ShouldBe("$.output.text");
    }

    [Fact]
    public void SerializeJson_RoundTrip_PreservesStructure()
    {
        var json = """
        {
            "name": "test",
            "nodes": [
                { "id": "a", "agent": "agent-a" },
                { "id": "b", "agent": "agent-b" }
            ],
            "edges": [
                { "from": "a", "to": "b" }
            ]
        }
        """;

        var graph = TaskGraphSerializer.DeserializeJson(json);
        graph.ShouldNotBeNull();

        var serialized = TaskGraphSerializer.SerializeJson(graph!);
        var roundTripped = TaskGraphSerializer.DeserializeJson(serialized);

        roundTripped.ShouldNotBeNull();
        roundTripped!.Name.ShouldBe("test");
        roundTripped.Nodes.Count.ShouldBe(2);
        roundTripped.Edges.Count.ShouldBe(1);
        roundTripped.Edges[0].FromNodeId.ShouldBe("a");
        roundTripped.Edges[0].ToNodeId.ShouldBe("b");
    }

    [Fact]
    public void DeserializeJson_NodesWithoutInput_InputIsNull()
    {
        var json = """
        {
            "name": "test",
            "nodes": [{ "id": "a", "agent": "agent-a" }],
            "edges": []
        }
        """;

        var graph = TaskGraphSerializer.DeserializeJson(json);

        graph.ShouldNotBeNull();
        graph!.Nodes[0].Input.ShouldBeNull();
    }
}
