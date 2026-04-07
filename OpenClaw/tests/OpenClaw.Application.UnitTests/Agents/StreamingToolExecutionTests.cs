using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class StreamingToolExecutionTests
{
    [Fact]
    public async Task ExecuteStreamAsync_StreamingTool_ShouldEmitProgressEvents()
    {
        // Arrange
        var mockProvider = Substitute.For<ILlmProvider>();
        var mockFactory = Substitute.For<ILlmProviderFactory>();
        mockFactory.GetProviderAsync(Arg.Any<CancellationToken>()).Returns(mockProvider);

        var streamingTool = new FakeStreamingTool();

        // LLM returns a tool call on first iteration, then final response
        var firstChunks = ToAsyncEnumerable(
            new ChatResponseChunk(ToolCall: new ToolCall("call_1", "streaming_tool", "{}")),
            new ChatResponseChunk(IsComplete: true));

        var secondChunks = ToAsyncEnumerable(
            new ChatResponseChunk(ContentDelta: "Done"),
            new ChatResponseChunk(IsComplete: true));

        var callCount = 0;
        mockProvider.ChatStreamAsync(
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => callCount++ == 0 ? firstChunks : secondChunks);

        var pipeline = new AgentPipeline(mockFactory, [streamingTool], new AgentPipelineOptions());

        // Act
        var events = new List<AgentStreamEvent>();
        await foreach (var evt in pipeline.ExecuteStreamAsync("test"))
        {
            events.Add(evt);
        }

        // Assert — should have ToolProgress events between ToolExecuting and ToolCompleted
        var toolEvents = events.Where(e =>
            e.Type is AgentStreamEventType.ToolExecuting
                or AgentStreamEventType.ToolProgress
                or AgentStreamEventType.ToolCompleted).ToList();

        toolEvents.Count.ShouldBe(4); // Executing, Progress("Step 1"), Progress("Step 2"), Completed
        toolEvents[0].Type.ShouldBe(AgentStreamEventType.ToolExecuting);
        toolEvents[1].Type.ShouldBe(AgentStreamEventType.ToolProgress);
        toolEvents[1].Content.ShouldBe("Step 1 of 2");
        toolEvents[2].Type.ShouldBe(AgentStreamEventType.ToolProgress);
        toolEvents[2].Content.ShouldBe("Step 2 of 2");
        toolEvents[3].Type.ShouldBe(AgentStreamEventType.ToolCompleted);
        toolEvents[3].Content.ShouldBe("result data");
    }

    [Fact]
    public async Task ExecuteStreamAsync_NonStreamingTool_ShouldWorkAsBlockingCall()
    {
        // Arrange
        var mockProvider = Substitute.For<ILlmProvider>();
        var mockFactory = Substitute.For<ILlmProviderFactory>();
        mockFactory.GetProviderAsync(Arg.Any<CancellationToken>()).Returns(mockProvider);

        var blockingTool = Substitute.For<IAgentTool>();
        blockingTool.Name.Returns("blocking_tool");
        blockingTool.Description.Returns("A blocking tool");
        blockingTool.Parameters.Returns((object?)null);
        blockingTool.ExecuteAsync(Arg.Any<ToolContext>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("blocking result"));

        var firstChunks = ToAsyncEnumerable(
            new ChatResponseChunk(ToolCall: new ToolCall("call_1", "blocking_tool", "{}")),
            new ChatResponseChunk(IsComplete: true));

        var secondChunks = ToAsyncEnumerable(
            new ChatResponseChunk(ContentDelta: "Done"),
            new ChatResponseChunk(IsComplete: true));

        var callCount = 0;
        mockProvider.ChatStreamAsync(
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(),
            Arg.Any<CancellationToken>())
            .Returns(_ => callCount++ == 0 ? firstChunks : secondChunks);

        var pipeline = new AgentPipeline(mockFactory, [blockingTool], new AgentPipelineOptions());

        // Act
        var events = new List<AgentStreamEvent>();
        await foreach (var evt in pipeline.ExecuteStreamAsync("test"))
        {
            events.Add(evt);
        }

        // Assert — no ToolProgress, just Executing → Completed
        var toolEvents = events.Where(e =>
            e.Type is AgentStreamEventType.ToolExecuting
                or AgentStreamEventType.ToolProgress
                or AgentStreamEventType.ToolCompleted).ToList();

        toolEvents.Count.ShouldBe(2);
        toolEvents[0].Type.ShouldBe(AgentStreamEventType.ToolExecuting);
        toolEvents[1].Type.ShouldBe(AgentStreamEventType.ToolCompleted);
        toolEvents[1].Content.ShouldBe("blocking result");
    }

    // --- Helpers ---

    private class FakeStreamingTool : IStreamingAgentTool
    {
        public string Name => "streaming_tool";
        public string Description => "A tool that streams progress";
        public object? Parameters => null;

        public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct = default)
            => Task.FromResult(ToolResult.Success("fallback"));

        public async IAsyncEnumerable<ToolProgress> ExecuteStreamAsync(
            ToolContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return new ToolProgress(ToolProgressType.Started);
            yield return new ToolProgress(ToolProgressType.InProgress, "Step 1 of 2");
            yield return new ToolProgress(ToolProgressType.InProgress, "Step 2 of 2");
            yield return new ToolProgress(ToolProgressType.Completed, Result: ToolResult.Success("result data"));
            await Task.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (var item in items) yield return item;
        await Task.CompletedTask;
    }
}
