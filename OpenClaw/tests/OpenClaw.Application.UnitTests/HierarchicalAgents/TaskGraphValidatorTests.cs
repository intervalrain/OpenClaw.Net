using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class TaskGraphValidatorTests
{
    [Fact]
    public void Validate_EmptyGraph_ReturnsError()
    {
        var graph = CreateGraph("empty", [], []);

        var errors = TaskGraphValidator.Validate(graph);

        errors.ShouldContain(e => e.Contains("at least one node"));
    }

    [Fact]
    public void Validate_SingleNode_NoErrors()
    {
        var graph = CreateGraph("single", [CreateNode("a")], []);

        var errors = TaskGraphValidator.Validate(graph);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DuplicateNodeIds_ReturnsError()
    {
        var graph = CreateGraph("dup", [CreateNode("a"), CreateNode("a")], []);

        var errors = TaskGraphValidator.Validate(graph);

        errors.ShouldContain(e => e.Contains("Duplicate node ID"));
    }

    [Fact]
    public void Validate_EdgeReferencesUnknownNode_ReturnsError()
    {
        var graph = CreateGraph("bad-edge",
            [CreateNode("a")],
            [new TaskEdge { FromNodeId = "a", ToNodeId = "missing" }]);

        var errors = TaskGraphValidator.Validate(graph);

        errors.ShouldContain(e => e.Contains("unknown target node"));
    }

    [Fact]
    public void Validate_SelfLoop_ReturnsError()
    {
        var graph = CreateGraph("self-loop",
            [CreateNode("a")],
            [new TaskEdge { FromNodeId = "a", ToNodeId = "a" }]);

        var errors = TaskGraphValidator.Validate(graph);

        errors.ShouldContain(e => e.Contains("Self-loop"));
    }

    [Fact]
    public void Validate_CyclicGraph_ReturnsError()
    {
        var graph = CreateGraph("cycle",
            [CreateNode("a"), CreateNode("b"), CreateNode("c")],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "b" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "c" },
                new TaskEdge { FromNodeId = "c", ToNodeId = "a" }
            ]);

        var errors = TaskGraphValidator.Validate(graph);

        errors.ShouldContain(e => e.Contains("cycle"));
    }

    [Fact]
    public void Validate_ValidDag_NoErrors()
    {
        // Diamond: a → b, a → c, b → d, c → d
        var graph = CreateGraph("diamond",
            [CreateNode("a"), CreateNode("b"), CreateNode("c"), CreateNode("d")],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "b" },
                new TaskEdge { FromNodeId = "a", ToNodeId = "c" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "d" },
                new TaskEdge { FromNodeId = "c", ToNodeId = "d" }
            ]);

        var errors = TaskGraphValidator.Validate(graph);

        errors.ShouldBeEmpty();
    }

    [Fact]
    public void TopologicalSort_LinearChain_ReturnsCorrectOrder()
    {
        var graph = CreateGraph("chain",
            [CreateNode("a"), CreateNode("b"), CreateNode("c")],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "b" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "c" }
            ]);

        var sorted = TaskGraphValidator.TopologicalSort(graph);

        sorted.ShouldNotBeNull();
        sorted!.Count.ShouldBe(3);
        sorted.IndexOf("a").ShouldBeLessThan(sorted.IndexOf("b"));
        sorted.IndexOf("b").ShouldBeLessThan(sorted.IndexOf("c"));
    }

    [Fact]
    public void TopologicalSort_CyclicGraph_ReturnsNull()
    {
        var graph = CreateGraph("cycle",
            [CreateNode("a"), CreateNode("b")],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "b" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "a" }
            ]);

        var sorted = TaskGraphValidator.TopologicalSort(graph);

        sorted.ShouldBeNull();
    }

    [Fact]
    public void TopologicalSort_DiamondDag_RespectsOrder()
    {
        var graph = CreateGraph("diamond",
            [CreateNode("a"), CreateNode("b"), CreateNode("c"), CreateNode("d")],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "b" },
                new TaskEdge { FromNodeId = "a", ToNodeId = "c" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "d" },
                new TaskEdge { FromNodeId = "c", ToNodeId = "d" }
            ]);

        var sorted = TaskGraphValidator.TopologicalSort(graph);

        sorted.ShouldNotBeNull();
        sorted!.Count.ShouldBe(4);
        sorted.IndexOf("a").ShouldBeLessThan(sorted.IndexOf("b"));
        sorted.IndexOf("a").ShouldBeLessThan(sorted.IndexOf("c"));
        sorted.IndexOf("b").ShouldBeLessThan(sorted.IndexOf("d"));
        sorted.IndexOf("c").ShouldBeLessThan(sorted.IndexOf("d"));
    }

    [Fact]
    public void GetUpstreamNodes_ReturnsDirectDependencies()
    {
        var graph = CreateGraph("test",
            [CreateNode("a"), CreateNode("b"), CreateNode("c")],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "c" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "c" }
            ]);

        var upstream = TaskGraphValidator.GetUpstreamNodes(graph, "c");

        upstream.Count.ShouldBe(2);
        upstream.ShouldContain("a");
        upstream.ShouldContain("b");
    }

    [Fact]
    public void GetDownstreamNodes_ReturnsDirectDependents()
    {
        var graph = CreateGraph("test",
            [CreateNode("a"), CreateNode("b"), CreateNode("c")],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "b" },
                new TaskEdge { FromNodeId = "a", ToNodeId = "c" }
            ]);

        var downstream = TaskGraphValidator.GetDownstreamNodes(graph, "a");

        downstream.Count.ShouldBe(2);
        downstream.ShouldContain("b");
        downstream.ShouldContain("c");
    }

    private static TaskGraph CreateGraph(string name, List<TaskNode> nodes, List<TaskEdge> edges) =>
        new() { Name = name, Nodes = nodes, Edges = edges };

    private static TaskNode CreateNode(string id) =>
        new() { Id = id, AgentName = $"agent-{id}" };
}
