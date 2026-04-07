using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class SubAgentToolTests
{
    private readonly ILlmProviderFactory _mockFactory;
    private readonly ILlmProvider _mockProvider;

    public SubAgentToolTests()
    {
        _mockProvider = Substitute.For<ILlmProvider>();
        _mockProvider.MaxContextTokens.Returns(128_000);
        _mockFactory = Substitute.For<ILlmProviderFactory>();
        _mockFactory.GetProviderAsync(Arg.Any<CancellationToken>()).Returns(_mockProvider);
    }

    [Fact]
    public async Task SpawnAgent_ShouldExecuteTaskAndReturnResult()
    {
        // LLM returns a direct answer (no tool calls)
        _mockProvider.ChatAsync(
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmChatResponse("Sub-agent result: files found."));

        var tool = new SubAgentTool(_mockFactory, []);
        var ctx = new ToolContext("""{"task": "Find all .cs files"}""");

        var result = await tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();
        result.Output!.ShouldContain("Sub-agent result");
    }

    [Fact]
    public async Task SpawnAgent_MaxDepthReached_ShouldFail()
    {
        var tool = new SubAgentTool(_mockFactory, [], currentDepth: 3, maxDepth: 3);
        var ctx = new ToolContext("""{"task": "nested task"}""");

        var result = await tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("Maximum sub-agent depth");
    }

    [Fact]
    public async Task SpawnAgent_EmptyTask_ShouldFail()
    {
        var tool = new SubAgentTool(_mockFactory, []);
        var ctx = new ToolContext("""{"task": ""}""");

        var result = await tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("task parameter is required");
    }

    [Fact]
    public async Task SpawnAgent_WithToolSubset_ShouldOnlyPassRequestedTools()
    {
        var readTool = Substitute.For<IAgentTool>();
        readTool.Name.Returns("read_file");
        readTool.Description.Returns("Read a file");

        var writeTool = Substitute.For<IAgentTool>();
        writeTool.Name.Returns("write_file");
        writeTool.Description.Returns("Write a file");

        _mockProvider.ChatAsync(
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmChatResponse("Done"));

        var tool = new SubAgentTool(_mockFactory, [readTool, writeTool]);
        var ctx = new ToolContext("""{"task": "read only", "tools": ["read_file"]}""");

        var result = await tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();

        // Verify: LLM was called with tool definitions that include read_file but not write_file
        // (plus spawn_agent which is always added as child)
        await _mockProvider.Received().ChatAsync(
            Arg.Any<IReadOnlyList<ChatMessage>>(),
            Arg.Is<IReadOnlyList<ToolDefinition>?>(tools =>
                tools != null &&
                tools.Any(t => t.Name == "read_file") &&
                !tools.Any(t => t.Name == "write_file")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SpawnAgent_InheritsUserContext()
    {
        var userId = Guid.NewGuid();
        var wsId = Guid.NewGuid();

        // Pipeline calls GetProviderAsync(Guid, CancellationToken) for userId
        _mockFactory.GetProviderAsync(userId, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(_mockProvider);
        _mockProvider.ChatAsync(
                Arg.Any<IReadOnlyList<ChatMessage>>(),
                Arg.Any<IReadOnlyList<ToolDefinition>?>(),
                Arg.Any<CancellationToken>())
            .Returns(new LlmChatResponse("Done"));

        var tool = new SubAgentTool(_mockFactory, []);
        var ctx = new ToolContext("""{"task": "check something"}""")
        {
            UserId = userId,
            WorkspaceId = wsId,
            Roles = ["Admin"]
        };

        var result = await tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();
        await _mockFactory.Received().GetProviderAsync(userId, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
