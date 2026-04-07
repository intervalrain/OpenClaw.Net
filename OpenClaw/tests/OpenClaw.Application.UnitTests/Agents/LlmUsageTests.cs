using OpenClaw.Contracts.Llm;

namespace OpenClaw.Application.UnitTests.Agents;

public class LlmUsageTests
{
    [Fact]
    public void Empty_ShouldHaveZeroTokens()
    {
        var usage = LlmUsage.Empty;

        usage.InputTokens.ShouldBe(0);
        usage.OutputTokens.ShouldBe(0);
        usage.TotalTokens.ShouldBe(0);
        usage.CacheReadTokens.ShouldBeNull();
        usage.CacheCreationTokens.ShouldBeNull();
    }

    [Fact]
    public void TotalTokens_ShouldSumInputAndOutput()
    {
        var usage = new LlmUsage(InputTokens: 100, OutputTokens: 50);

        usage.TotalTokens.ShouldBe(150);
    }

    [Fact]
    public void OperatorPlus_ShouldAccumulateAllFields()
    {
        var a = new LlmUsage(100, 50, CacheReadTokens: 10, CacheCreationTokens: 5);
        var b = new LlmUsage(200, 80, CacheReadTokens: 20, CacheCreationTokens: 15);

        var result = a + b;

        result.InputTokens.ShouldBe(300);
        result.OutputTokens.ShouldBe(130);
        result.CacheReadTokens.ShouldBe(30);
        result.CacheCreationTokens.ShouldBe(20);
        result.TotalTokens.ShouldBe(430);
    }

    [Fact]
    public void OperatorPlus_WithNullCacheTokens_ShouldTreatAsZero()
    {
        var a = new LlmUsage(100, 50); // CacheReadTokens = null
        var b = new LlmUsage(200, 80, CacheReadTokens: 20);

        var result = a + b;

        result.CacheReadTokens.ShouldBe(20); // 0 + 20
    }
}
