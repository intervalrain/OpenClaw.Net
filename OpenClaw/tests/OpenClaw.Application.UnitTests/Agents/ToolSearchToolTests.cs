using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class ToolSearchToolTests
{
    private readonly ToolSearchTool _tool;

    public ToolSearchToolTests()
    {
        var tools = new List<IAgentTool>
        {
            CreateMockTool("read_file", "Read a file from the filesystem"),
            CreateMockTool("write_file", "Write content to a file"),
            CreateMockTool("execute_shell", "Execute a shell command"),
            CreateMockTool("web_search", "Search the web for information"),
            CreateMockTool("git_status", "Show git repository status"),
        };
        _tool = new ToolSearchTool(tools);
    }

    [Fact]
    public async Task Search_ByName_ShouldFindMatchingTools()
    {
        var ctx = new ToolContext("""{"query": "file"}""");
        var result = await _tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();
        result.Output.ShouldNotBeNull();
        result.Output.ShouldContain("read_file");
        result.Output.ShouldContain("write_file");
        result.Output.ShouldNotContain("execute_shell");
    }

    [Fact]
    public async Task Search_ByDescription_ShouldFindMatchingTools()
    {
        var ctx = new ToolContext("""{"query": "shell"}""");
        var result = await _tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();
        result.Output.ShouldNotBeNull();
        result.Output.ShouldContain("execute_shell");
    }

    [Fact]
    public async Task Search_NoMatch_ShouldReturnAllToolNames()
    {
        var ctx = new ToolContext("""{"query": "nonexistent"}""");
        var result = await _tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();
        result.Output.ShouldNotBeNull();
        result.Output.ShouldContain("No tools matched");
        result.Output.ShouldContain("read_file"); // lists all available
    }

    [Fact]
    public async Task Search_EmptyQuery_ShouldReturnError()
    {
        var ctx = new ToolContext("""{"query": ""}""");
        var result = await _tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task Search_WithMaxResults_ShouldLimit()
    {
        var ctx = new ToolContext("""{"query": "file", "max_results": 1}""");
        var result = await _tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();
        result.Output.ShouldNotBeNull();
        result.Output.ShouldContain("Found 1 tool");
    }

    [Fact]
    public async Task Search_ReturnsParameterSchemas()
    {
        var ctx = new ToolContext("""{"query": "git"}""");
        var result = await _tool.ExecuteAsync(ctx);

        result.IsSuccess.ShouldBeTrue();
        result.Output.ShouldNotBeNull();
        result.Output.ShouldContain("git_status");
        result.Output.ShouldContain("parameters"); // schema included
    }

    private static IAgentTool CreateMockTool(string name, string description)
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns(name);
        tool.Description.Returns(description);
        tool.Parameters.Returns(new { type = "object" });
        return tool;
    }
}
