using System.Text.Json;
using NSubstitute;
using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class SafetyAndObservabilityTests
{
    [Fact]
    public async Task BudgetLimit_WhenExhausted_ReturnsFailure()
    {
        var agent = new TestDeterministicAgent("test", AgentResult.Success(JsonDocument.Parse("{}")));

        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions { BudgetLimit = 100m }
        };

        // Simulate already-used budget
        context.AddTokensUsed(150m);

        var result = await agent.ExecuteAsync(context);

        result.Status.ShouldBe(AgentResultStatus.Failed);
        result.ErrorMessage!.ShouldContain("budget exhausted");
    }

    [Fact]
    public async Task BudgetLimit_WhenNotExhausted_Succeeds()
    {
        var agent = new TestDeterministicAgent("test", AgentResult.Success(JsonDocument.Parse("{}")));

        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions { BudgetLimit = 1000m }
        };

        context.AddTokensUsed(50m);

        var result = await agent.ExecuteAsync(context);

        result.Status.ShouldBe(AgentResultStatus.Success);
    }

    [Fact]
    public async Task TokenTracking_AccumulatesAcrossCalls()
    {
        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions()
        };

        var agent1 = new TestDeterministicAgent("a", AgentResult.Success(JsonDocument.Parse("{}"), tokensUsed: 100));
        var agent2 = new TestDeterministicAgent("b", AgentResult.Success(JsonDocument.Parse("{}"), tokensUsed: 200));

        await agent1.ExecuteAsync(context);
        await agent2.ExecuteAsync(context);

        context.TokensUsed.ShouldBe(300m);
    }

    [Fact]
    public void TokenTracking_ThreadSafe()
    {
        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions()
        };

        // Parallel increments
        Parallel.For(0, 1000, _ => context.AddTokensUsed(1m));

        context.TokensUsed.ShouldBe(1000m);
    }

    [Fact]
    public void Timeline_RecordsEvents()
    {
        var timeline = new AgentExecutionTimeline();

        timeline.Record("agent-a", AgentTimelineEventType.Started, "Node 'a'");
        timeline.Record("agent-a", AgentTimelineEventType.Completed, "tokens=100");

        var events = timeline.GetEvents();

        events.Count.ShouldBe(2);
        events[0].AgentName.ShouldBe("agent-a");
        events[0].Type.ShouldBe(AgentTimelineEventType.Started);
        events[1].Type.ShouldBe(AgentTimelineEventType.Completed);
    }

    [Fact]
    public void Timeline_ThreadSafe()
    {
        var timeline = new AgentExecutionTimeline();

        Parallel.For(0, 100, i =>
        {
            timeline.Record($"agent-{i}", AgentTimelineEventType.Started);
        });

        timeline.GetEvents().Count.ShouldBe(100);
    }

    [Fact]
    public void DepthTracking_NestedContexts()
    {
        var root = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions()
        };

        var child = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions(),
            Parent = root
        };

        var grandchild = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions(),
            Parent = child
        };

        root.Depth.ShouldBe(0);
        child.Depth.ShouldBe(1);
        grandchild.Depth.ShouldBe(2);
    }

    [Fact]
    public async Task Timeout_WhenExceeded_ReturnsFailure()
    {
        var slowAgent = new SlowAgent();

        var context = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions { Timeout = TimeSpan.FromMilliseconds(50) }
        };

        var result = await slowAgent.ExecuteAsync(context);

        result.Status.ShouldBe(AgentResultStatus.Failed);
        result.ErrorMessage!.ShouldContain("timed out");
    }

    // --- Test Helpers ---

    private class TestDeterministicAgent(string name, AgentResult result) : DeterministicAgent
    {
        public override string Name => name;
        public override string Description => "test";

        protected override Task<AgentResult> ExecuteCoreAsync(
            AgentExecutionContext context, CancellationToken ct)
        {
            return Task.FromResult(result);
        }
    }

    private class SlowAgent : DeterministicAgent
    {
        public override string Name => "slow";
        public override string Description => "A slow agent for testing timeouts";

        protected override async Task<AgentResult> ExecuteCoreAsync(
            AgentExecutionContext context, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return AgentResult.Success(JsonDocument.Parse("{}"));
        }
    }
}
