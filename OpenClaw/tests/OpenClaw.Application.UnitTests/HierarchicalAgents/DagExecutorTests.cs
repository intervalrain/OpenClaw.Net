using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class DagExecutorTests
{
    private readonly IAgentRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly DagExecutor _executor;

    public DagExecutorTests()
    {
        _registry = Substitute.For<IAgentRegistry>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<DagExecutor>>();
        _executor = new DagExecutor(_registry, _serviceProvider, logger);
    }

    [Fact]
    public async Task ExecuteAsync_SingleNode_ExecutesSuccessfully()
    {
        var agent = CreateMockAgent("agent-a", AgentResult.Success(JsonDocument.Parse("{\"value\":42}")));
        _registry.GetAgent("agent-a").Returns(agent);

        var graph = CreateGraph("single",
            [new TaskNode { Id = "a", AgentName = "agent-a" }],
            []);

        var result = await _executor.ExecuteAsync(graph, new AgentExecutionOptions());

        result.IsSuccess.ShouldBeTrue();
        result.Nodes.Count.ShouldBe(1);
        result.Nodes[0].Status.ShouldBe(TaskNodeStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_LinearChain_ExecutesInOrder()
    {
        var executionOrder = new List<string>();

        SetupAgent("agent-a", executionOrder);
        SetupAgent("agent-b", executionOrder);
        SetupAgent("agent-c", executionOrder);

        var graph = CreateGraph("chain",
            [
                new TaskNode { Id = "a", AgentName = "agent-a" },
                new TaskNode { Id = "b", AgentName = "agent-b" },
                new TaskNode { Id = "c", AgentName = "agent-c" }
            ],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "b" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "c" }
            ]);

        var result = await _executor.ExecuteAsync(graph, new AgentExecutionOptions());

        result.IsSuccess.ShouldBeTrue();
        executionOrder.Count.ShouldBe(3);
        executionOrder.IndexOf("agent-a").ShouldBeLessThan(executionOrder.IndexOf("agent-b"));
        executionOrder.IndexOf("agent-b").ShouldBeLessThan(executionOrder.IndexOf("agent-c"));
    }

    [Fact]
    public async Task ExecuteAsync_ParallelNodes_BothExecute()
    {
        SetupAgent("agent-a", []);
        SetupAgent("agent-b", []);
        SetupAgent("agent-c", []);

        // a → c, b → c (a and b can run in parallel)
        var graph = CreateGraph("parallel",
            [
                new TaskNode { Id = "a", AgentName = "agent-a" },
                new TaskNode { Id = "b", AgentName = "agent-b" },
                new TaskNode { Id = "c", AgentName = "agent-c" }
            ],
            [
                new TaskEdge { FromNodeId = "a", ToNodeId = "c" },
                new TaskEdge { FromNodeId = "b", ToNodeId = "c" }
            ]);

        var result = await _executor.ExecuteAsync(graph, new AgentExecutionOptions());

        result.IsSuccess.ShouldBeTrue();
        result.Nodes.ShouldAllBe(n => n.Status == TaskNodeStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_FailedNode_SkipsDownstream()
    {
        var failAgent = CreateMockAgent("agent-a",
            AgentResult.Failed("something broke"));
        _registry.GetAgent("agent-a").Returns(failAgent);
        SetupAgent("agent-b", []);

        var graph = CreateGraph("fail",
            [
                new TaskNode { Id = "a", AgentName = "agent-a" },
                new TaskNode { Id = "b", AgentName = "agent-b" }
            ],
            [new TaskEdge { FromNodeId = "a", ToNodeId = "b" }]);

        var result = await _executor.ExecuteAsync(graph, new AgentExecutionOptions());

        result.IsSuccess.ShouldBeFalse();
        graph.Nodes[0].Status.ShouldBe(TaskNodeStatus.Failed);
        graph.Nodes[1].Status.ShouldBe(TaskNodeStatus.Skipped);
    }

    [Fact]
    public async Task ExecuteAsync_MissingAgent_NodeFails()
    {
        _registry.GetAgent("missing-agent").Returns((IAgent?)null);

        var graph = CreateGraph("missing",
            [new TaskNode { Id = "a", AgentName = "missing-agent" }],
            []);

        var result = await _executor.ExecuteAsync(graph, new AgentExecutionOptions());

        result.IsSuccess.ShouldBeFalse();
        graph.Nodes[0].Status.ShouldBe(TaskNodeStatus.Failed);
        graph.Nodes[0].ErrorMessage.ShouldContain("not found");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidGraph_ReturnsFailure()
    {
        var graph = CreateGraph("empty", [], []);

        var result = await _executor.ExecuteAsync(graph, new AgentExecutionOptions());

        result.IsSuccess.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("at least one node");
    }

    [Fact]
    public async Task ExecuteAsync_OutputMapping_PassesDataDownstream()
    {
        var outputDoc = JsonDocument.Parse("{\"result\":{\"script\":\"hello world\"}}");
        var agentA = CreateMockAgent("agent-a", AgentResult.Success(outputDoc));
        _registry.GetAgent("agent-a").Returns(agentA);

        AgentExecutionContext? capturedContext = null;
        var agentB = Substitute.For<IAgent>();
        agentB.Name.Returns("agent-b");
        agentB.ExecuteAsync(Arg.Do<AgentExecutionContext>(ctx => capturedContext = ctx), Arg.Any<CancellationToken>())
            .Returns(AgentResult.Success(JsonDocument.Parse("{}")));
        _registry.GetAgent("agent-b").Returns(agentB);

        var graph = CreateGraph("mapping",
            [
                new TaskNode { Id = "a", AgentName = "agent-a" },
                new TaskNode { Id = "b", AgentName = "agent-b" }
            ],
            [new TaskEdge { FromNodeId = "a", ToNodeId = "b", OutputMapping = "$.result.script" }]);

        await _executor.ExecuteAsync(graph, new AgentExecutionOptions());

        capturedContext.ShouldNotBeNull();
        capturedContext!.Input.RootElement.GetProperty("script").GetString().ShouldBe("hello world");
    }

    [Fact]
    public void ResolveJsonPath_SimpleProperty_ReturnsValue()
    {
        var doc = JsonDocument.Parse("{\"name\":\"test\"}");
        var result = DagExecutor.ResolveJsonPath(doc, "$.name");

        result.ShouldNotBeNull();
        result!.Value.GetString().ShouldBe("test");
    }

    [Fact]
    public void ResolveJsonPath_NestedProperty_ReturnsValue()
    {
        var doc = JsonDocument.Parse("{\"result\":{\"script\":\"hello\"}}");
        var result = DagExecutor.ResolveJsonPath(doc, "$.result.script");

        result.ShouldNotBeNull();
        result!.Value.GetString().ShouldBe("hello");
    }

    [Fact]
    public void ResolveJsonPath_MissingProperty_ReturnsNull()
    {
        var doc = JsonDocument.Parse("{\"name\":\"test\"}");
        var result = DagExecutor.ResolveJsonPath(doc, "$.missing");

        result.ShouldBeNull();
    }

    private void SetupAgent(string name, List<string> executionOrder)
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.ExecuteAsync(Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                lock (executionOrder)
                {
                    executionOrder.Add(name);
                }
                return AgentResult.Success(JsonDocument.Parse($"{{\"agent\":\"{name}\"}}"));
            });
        _registry.GetAgent(name).Returns(agent);
    }

    private static IAgent CreateMockAgent(string name, AgentResult result)
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.ExecuteAsync(Arg.Any<AgentExecutionContext>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return agent;
    }

    private static TaskGraph CreateGraph(string name, List<TaskNode> nodes, List<TaskEdge> edges) =>
        new() { Name = name, Nodes = nodes, Edges = edges };
}
