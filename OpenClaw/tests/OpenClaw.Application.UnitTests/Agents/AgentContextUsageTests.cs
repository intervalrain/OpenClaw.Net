using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class AgentContextUsageTests
{
    private static AgentContext CreateContext() => new()
    {
        UserInput = "test",
        LlmProvider = Substitute.For<ILlmProvider>(),
        Skills = [],
        Options = new AgentPipelineOptions()
    };

    [Fact]
    public void TotalUsage_ShouldStartAtEmpty()
    {
        var ctx = CreateContext();

        ctx.TotalUsage.ShouldBe(LlmUsage.Empty);
    }

    [Fact]
    public void AccumulateUsage_ShouldSumMultipleCalls()
    {
        var ctx = CreateContext();

        ctx.AccumulateUsage(new LlmUsage(100, 50));
        ctx.AccumulateUsage(new LlmUsage(200, 80));

        ctx.TotalUsage.InputTokens.ShouldBe(300);
        ctx.TotalUsage.OutputTokens.ShouldBe(130);
    }

    [Fact]
    public void AccumulateUsage_WithNull_ShouldBeNoOp()
    {
        var ctx = CreateContext();
        ctx.AccumulateUsage(new LlmUsage(100, 50));

        ctx.AccumulateUsage(null);

        ctx.TotalUsage.InputTokens.ShouldBe(100);
        ctx.TotalUsage.OutputTokens.ShouldBe(50);
    }
}
