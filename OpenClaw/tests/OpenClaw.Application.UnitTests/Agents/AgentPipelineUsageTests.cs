using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class AgentPipelineUsageTests
{
    [Fact]
    public async Task ExecuteStreamAsync_ShouldEmitUsageReport_WhenLlmReturnsUsage()
    {
        // Arrange
        var mockProvider = Substitute.For<ILlmProvider>();
        var mockFactory = Substitute.For<ILlmProviderFactory>();
        mockFactory.GetProviderAsync(Arg.Any<CancellationToken>()).Returns(mockProvider);

        var usage = new LlmUsage(InputTokens: 150, OutputTokens: 42);

        // Simulate streaming: one content chunk + final chunk with usage
        var chunks = ToAsyncEnumerable(
            new ChatResponseChunk(ContentDelta: "Hello"),
            new ChatResponseChunk(IsComplete: true, Usage: usage));

        mockProvider.ChatStreamAsync(
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(),
            Arg.Any<CancellationToken>())
            .Returns(chunks);

        var pipeline = new AgentPipeline(mockFactory, [], new AgentPipelineOptions());

        // Act
        var events = new List<AgentStreamEvent>();
        await foreach (var evt in pipeline.ExecuteStreamAsync("hi"))
        {
            events.Add(evt);
        }

        // Assert
        var usageEvent = events.FirstOrDefault(e => e.Type == AgentStreamEventType.UsageReport);
        usageEvent.ShouldNotBeNull();
        usageEvent.Usage.ShouldNotBeNull();
        usageEvent.Usage!.InputTokens.ShouldBe(150);
        usageEvent.Usage!.OutputTokens.ShouldBe(42);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldEmitUsageReport_EvenWithNoUsageData()
    {
        // Arrange
        var mockProvider = Substitute.For<ILlmProvider>();
        var mockFactory = Substitute.For<ILlmProviderFactory>();
        mockFactory.GetProviderAsync(Arg.Any<CancellationToken>()).Returns(mockProvider);

        // No usage on final chunk (e.g. Ollama)
        var chunks = ToAsyncEnumerable(
            new ChatResponseChunk(ContentDelta: "Hello"),
            new ChatResponseChunk(IsComplete: true));

        mockProvider.ChatStreamAsync(
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(),
            Arg.Any<CancellationToken>())
            .Returns(chunks);

        var pipeline = new AgentPipeline(mockFactory, [], new AgentPipelineOptions());

        // Act
        var events = new List<AgentStreamEvent>();
        await foreach (var evt in pipeline.ExecuteStreamAsync("hi"))
        {
            events.Add(evt);
        }

        // Assert — UsageReport should still be emitted with Empty usage
        var usageEvent = events.FirstOrDefault(e => e.Type == AgentStreamEventType.UsageReport);
        usageEvent.ShouldNotBeNull();
        usageEvent.Usage.ShouldNotBeNull();
        usageEvent.Usage!.TotalTokens.ShouldBe(0);
    }

    [Fact]
    public async Task ExecuteStreamAsync_ShouldAccumulateUsage_AcrossMultipleIterations()
    {
        // Arrange: LLM returns tool call on first iteration, then final response
        var mockProvider = Substitute.For<ILlmProvider>();
        var mockFactory = Substitute.For<ILlmProviderFactory>();
        mockFactory.GetProviderAsync(Arg.Any<CancellationToken>()).Returns(mockProvider);

        var mockTool = Substitute.For<IAgentTool>();
        mockTool.Name.Returns("test_tool");
        mockTool.Description.Returns("A test tool");
        mockTool.Parameters.Returns((object?)null);
        mockTool.ExecuteAsync(Arg.Any<ToolContext>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("done"));

        // First call: tool call + usage
        var firstChunks = ToAsyncEnumerable(
            new ChatResponseChunk(ToolCall: new ToolCall("call_1", "test_tool", "{}")),
            new ChatResponseChunk(IsComplete: true, Usage: new LlmUsage(100, 30)));

        // Second call: final response + usage
        var secondChunks = ToAsyncEnumerable(
            new ChatResponseChunk(ContentDelta: "Result"),
            new ChatResponseChunk(IsComplete: true, Usage: new LlmUsage(200, 50)));

        var callCount = 0;
        mockProvider.ChatStreamAsync(
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => callCount++ == 0 ? firstChunks : secondChunks);

        var pipeline = new AgentPipeline(mockFactory, [mockTool], new AgentPipelineOptions());

        // Act
        var events = new List<AgentStreamEvent>();
        await foreach (var evt in pipeline.ExecuteStreamAsync("use the tool"))
        {
            events.Add(evt);
        }

        // Assert — accumulated across 2 LLM calls
        var usageEvent = events.Last(e => e.Type == AgentStreamEventType.UsageReport);
        usageEvent.Usage.ShouldNotBeNull();
        usageEvent.Usage!.InputTokens.ShouldBe(300);  // 100 + 200
        usageEvent.Usage!.OutputTokens.ShouldBe(80);   // 30 + 50
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
