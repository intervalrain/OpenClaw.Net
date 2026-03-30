using System.Text.Json;
using NSubstitute;
using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Skills;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class ToolAgentTests
{
    [Fact]
    public void Name_PrefixedWithTool()
    {
        var tool = CreateMockTool("read_file");
        var agent = new ToolAgent(tool);

        agent.Name.ShouldBe("tool:read_file");
    }

    [Fact]
    public void Description_DelegatedToTool()
    {
        var tool = CreateMockTool("read_file", "Reads a file");
        var agent = new ToolAgent(tool);

        agent.Description.ShouldBe("Reads a file");
    }

    [Fact]
    public void ExecutionType_IsDeterministic()
    {
        var tool = CreateMockTool("test");
        var agent = new ToolAgent(tool);

        agent.ExecutionType.ShouldBe(AgentExecutionType.Deterministic);
    }

    [Fact]
    public void PreferredProvider_IsNull()
    {
        var tool = CreateMockTool("test");
        var agent = new ToolAgent(tool);

        agent.PreferredProvider.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsSuccessResult()
    {
        var tool = CreateMockTool("test");
        tool.ExecuteAsync(Arg.Any<ToolContext>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("hello world"));

        var agent = new ToolAgent(tool);
        var context = CreateContext(new { message = "test input" });

        var result = await agent.ExecuteAsync(context);

        result.Status.ShouldBe(AgentResultStatus.Success);
        result.Output.RootElement.GetProperty("output").GetString().ShouldBe("hello world");
    }

    [Fact]
    public async Task ExecuteAsync_Failure_ReturnsFailedResult()
    {
        var tool = CreateMockTool("test");
        tool.ExecuteAsync(Arg.Any<ToolContext>(), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Failure("something went wrong"));

        var agent = new ToolAgent(tool);
        var context = CreateContext(new { });

        var result = await agent.ExecuteAsync(context);

        result.Status.ShouldBe(AgentResultStatus.Failed);
        result.ErrorMessage.ShouldBe("something went wrong");
    }

    [Fact]
    public async Task ExecuteAsync_PassesInputAsArguments()
    {
        var tool = CreateMockTool("test");
        ToolContext? capturedContext = null;
        tool.ExecuteAsync(Arg.Do<ToolContext>(tc => capturedContext = tc), Arg.Any<CancellationToken>())
            .Returns(ToolResult.Success("ok"));

        var agent = new ToolAgent(tool);
        var input = new { path = "/tmp/test.txt" };
        var context = CreateContext(input);

        await agent.ExecuteAsync(context);

        capturedContext.ShouldNotBeNull();
        var parsed = JsonDocument.Parse(capturedContext!.Arguments!);
        parsed.RootElement.GetProperty("path").GetString().ShouldBe("/tmp/test.txt");
    }

    [Fact]
    public async Task ExecuteAsync_ExceedsMaxDepth_ReturnsFailed()
    {
        var tool = CreateMockTool("test");
        var agent = new ToolAgent(tool);

        var parentContext = CreateContext(new { }, maxDepth: 2);
        var childContext = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions { MaxDepth = 2 },
            Parent = parentContext
        };
        var grandchildContext = new AgentExecutionContext
        {
            Input = JsonDocument.Parse("{}"),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions { MaxDepth = 2 },
            Parent = childContext
        };

        var result = await agent.ExecuteAsync(grandchildContext);

        result.Status.ShouldBe(AgentResultStatus.Failed);
        result.ErrorMessage.ShouldContain("Max agent depth");
    }

    private static IAgentTool CreateMockTool(string name, string description = "A test tool")
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        tool.Parameters.Returns((object?)null);
        return tool;
    }

    private static AgentExecutionContext CreateContext(object input, int maxDepth = 5)
    {
        return new AgentExecutionContext
        {
            Input = JsonDocument.Parse(JsonSerializer.Serialize(input)),
            Services = Substitute.For<IServiceProvider>(),
            Options = new AgentExecutionOptions { MaxDepth = maxDepth }
        };
    }
}
