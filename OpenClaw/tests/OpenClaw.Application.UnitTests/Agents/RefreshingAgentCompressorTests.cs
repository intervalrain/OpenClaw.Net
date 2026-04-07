using Microsoft.Extensions.Logging;
using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Domain.Chat.Enums;

namespace OpenClaw.Application.UnitTests.Agents;

public class RefreshingAgentCompressorTests
{
    private readonly ILlmProvider _mockProvider;
    private readonly RefreshingAgentCompressor _compressor;

    public RefreshingAgentCompressorTests()
    {
        _mockProvider = Substitute.For<ILlmProvider>();
        _mockProvider.MaxContextTokens.Returns(128_000); // simulate GPT-4o
        var logger = Substitute.For<ILogger<RefreshingAgentCompressor>>();
        _compressor = new RefreshingAgentCompressor(logger);
    }

    [Fact]
    public async Task CompressIfNeeded_ShortHistory_ShouldReturnUnchanged()
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "Hello"),
            new(ChatRole.Assistant, "Hi there")
        };

        var result = await _compressor.CompressIfNeededAsync(messages, _mockProvider);

        result.ShouldBe(messages);
        // LLM should NOT have been called
        await _mockProvider.DidNotReceive().ChatAsync(
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Any<IReadOnlyList<ToolDefinition>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompressIfNeeded_UnderTokenThreshold_ShouldReturnUnchanged()
    {
        // 10 messages but very short content — under token threshold
        var messages = Enumerable.Range(0, 10)
            .Select(i => new ChatMessage(
                i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                $"Msg {i}"))
            .ToList();

        var result = await _compressor.CompressIfNeededAsync(messages, _mockProvider);

        result.ShouldBe(messages);
    }

    [Fact]
    public async Task CompressIfNeeded_OverThreshold_ShouldCallLlmAndCompress()
    {
        // 20 messages × 1000 chars / 2 ≈ 10000 tokens
        // With MaxContextTokens=128K, default 75% threshold = 96K → won't trigger
        // So set a small context window to trigger compression
        _mockProvider.MaxContextTokens.Returns(12_000); // 75% = 9000 threshold, 10K > 9K → triggers

        var messages = new List<ChatMessage>();
        for (int i = 0; i < 20; i++)
        {
            messages.Add(new ChatMessage(
                i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                new string('x', 1000)));
        }

        _mockProvider.ChatAsync(
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Is<IReadOnlyList<ToolDefinition>?>(t => t == null),
                Arg.Any<CancellationToken>())
            .Returns(new LlmChatResponse("Summary of earlier discussion."));

        var result = await _compressor.CompressIfNeededAsync(messages, _mockProvider);

        // Should have compressed: system summary + 6 recent messages
        result.Count.ShouldBe(7); // 1 summary + 6 recent
        result[0].Role.ShouldBe(ChatRole.System);
        result[0].Content!.ShouldContain("Conversation Summary");
        result[0].Content!.ShouldContain("Summary of earlier discussion.");

        // Recent messages preserved verbatim
        result[1].Content.ShouldBe(messages[14].Content);
    }

    [Fact]
    public async Task CompressIfNeeded_PreservesSystemMessages()
    {
        _mockProvider.MaxContextTokens.Returns(12_000);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are a helpful assistant.")
        };
        // Add enough non-system messages to trigger compression
        for (int i = 0; i < 20; i++)
        {
            messages.Add(new ChatMessage(
                i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                new string('y', 1000)));
        }

        _mockProvider.ChatAsync(
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmChatResponse("Compressed summary."));

        var result = await _compressor.CompressIfNeededAsync(messages, _mockProvider);

        // System message should be first
        result[0].Role.ShouldBe(ChatRole.System);
        result[0].Content.ShouldBe("You are a helpful assistant.");

        // Summary boundary second
        result[1].Role.ShouldBe(ChatRole.System);
        result[1].Content!.ShouldContain("Conversation Summary");
    }

    [Fact]
    public async Task CompressIfNeeded_LlmFails_ShouldFallbackToTruncation()
    {
        _mockProvider.MaxContextTokens.Returns(12_000);

        var messages = new List<ChatMessage>();
        for (int i = 0; i < 20; i++)
        {
            messages.Add(new ChatMessage(
                i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                new string('z', 1000)));
        }

        _mockProvider.ChatAsync(
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(),
                Arg.Any<CancellationToken>())
            .Returns<LlmChatResponse>(_ => throw new HttpRequestException("API error"));

        var result = await _compressor.CompressIfNeededAsync(messages, _mockProvider);

        // Should still compress, just with truncated fallback
        result.Count.ShouldBe(7); // 1 summary + 6 recent
        result[0].Role.ShouldBe(ChatRole.System);
        result[0].Content!.ShouldContain("truncated");
    }

    [Fact]
    public async Task CompressIfNeeded_CustomOptions_ShouldRespectThresholds()
    {
        // MaxContextTokens=128K, CompressAtPercentage=0.005 → threshold = 640 tokens
        // 10 messages × 200 chars / 2 = 1000 tokens > 640 → triggers
        _mockProvider.ChatAsync(
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmChatResponse("Short summary."));

        var messages = Enumerable.Range(0, 10)
            .Select(i => new ChatMessage(
                i % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
                new string('a', 200)))
            .ToList();

        var opts = new ContextCompressorOptions { CompressAtPercentage = 0.005, RecentMessagesToKeep = 4 };
        var result = await _compressor.CompressIfNeededAsync(messages, _mockProvider, opts);

        // 1 summary + 4 recent
        result.Count.ShouldBe(5);
    }
}
