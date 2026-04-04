using System.Text.Json;
using NSubstitute;
using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class AgentRegistryTests
{
    [Fact]
    public void GetAgent_WithRegisteredAgent_ReturnsAgent()
    {
        var agent = CreateMockAgent("test-agent");
        var registry = new AgentRegistry([agent]);

        var result = registry.GetAgent("test-agent");

        result.ShouldBe(agent);
    }

    [Fact]
    public void GetAgent_CaseInsensitive_ReturnsAgent()
    {
        var agent = CreateMockAgent("Test-Agent");
        var registry = new AgentRegistry([agent]);

        registry.GetAgent("test-agent").ShouldBe(agent);
        registry.GetAgent("TEST-AGENT").ShouldBe(agent);
    }

    [Fact]
    public void GetAgent_NotRegistered_ReturnsNull()
    {
        var registry = new AgentRegistry([]);

        registry.GetAgent("missing").ShouldBeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllRegisteredAgents()
    {
        var agents = new[]
        {
            CreateMockAgent("agent-a"),
            CreateMockAgent("agent-b"),
            CreateMockAgent("agent-c")
        };
        var registry = new AgentRegistry(agents);

        var all = registry.GetAll();

        all.Count.ShouldBe(3);
    }

    [Fact]
    public void Register_AddsNewAgent()
    {
        var registry = new AgentRegistry([]);
        var agent = CreateMockAgent("new-agent");

        registry.Register(agent);

        registry.GetAgent("new-agent").ShouldBe(agent);
        registry.GetAll().Count.ShouldBe(1);
    }

    [Fact]
    public void Register_SameName_OverwritesExisting()
    {
        var original = CreateMockAgent("agent");
        var replacement = CreateMockAgent("agent");
        var registry = new AgentRegistry([original]);

        registry.Register(replacement);

        registry.GetAgent("agent").ShouldBe(replacement);
        registry.GetAll().Count.ShouldBe(1);
    }

    private static IAgent CreateMockAgent(string name)
    {
        var agent = Substitute.For<IAgent>();
        agent.Name.Returns(name);
        agent.Description.Returns($"Mock agent: {name}");
        agent.Version.Returns("1.0");
        agent.ExecutionType.Returns(AgentExecutionType.Deterministic);
        return agent;
    }
}
