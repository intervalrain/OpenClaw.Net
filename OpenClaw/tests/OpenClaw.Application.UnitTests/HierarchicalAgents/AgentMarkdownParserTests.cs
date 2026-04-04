using OpenClaw.Application.HierarchicalAgents;
using OpenClaw.Contracts.HierarchicalAgents;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class AgentMarkdownParserTests
{
    [Fact]
    public void Parse_FullFrontmatter_AllFieldsParsed()
    {
        var markdown = """
            ---
            name: script-writer
            description: Generates video scripts from a topic
            version: v2
            type: llm
            provider: openai
            tools: [web_search, read_file]
            ---

            ## Instructions

            You are a professional script writer.
            """;

        var result = AgentMarkdownParser.Parse(markdown);

        result.Name.ShouldBe("script-writer");
        result.Description.ShouldBe("Generates video scripts from a topic");
        result.Version.ShouldBe("v2");
        result.ExecutionType.ShouldBe(AgentExecutionType.Llm);
        result.PreferredProvider.ShouldBe("openai");
        result.Tools.Count.ShouldBe(2);
        result.Tools.ShouldContain("web_search");
        result.Tools.ShouldContain("read_file");
        result.Instructions.ShouldContain("professional script writer");
    }

    [Fact]
    public void Parse_MinimalFrontmatter_UsesDefaults()
    {
        var markdown = """
            ---
            name: simple-agent
            description: A simple agent
            ---

            Do something.
            """;

        var result = AgentMarkdownParser.Parse(markdown);

        result.Name.ShouldBe("simple-agent");
        result.Version.ShouldBe("1.0");
        result.ExecutionType.ShouldBe(AgentExecutionType.Llm);
        result.PreferredProvider.ShouldBeNull();
        result.Tools.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_DeterministicType_ParsedCorrectly()
    {
        var markdown = """
            ---
            name: formatter
            description: Formats data
            type: deterministic
            ---

            Format the input.
            """;

        var result = AgentMarkdownParser.Parse(markdown);

        result.ExecutionType.ShouldBe(AgentExecutionType.Deterministic);
    }

    [Fact]
    public void Parse_HybridType_ParsedCorrectly()
    {
        var markdown = """
            ---
            name: smart-formatter
            description: Formats data with LLM fallback
            type: hybrid
            ---

            Format the input.
            """;

        var result = AgentMarkdownParser.Parse(markdown);

        result.ExecutionType.ShouldBe(AgentExecutionType.Hybrid);
    }

    [Fact]
    public void Parse_MissingName_ThrowsFormatException()
    {
        var markdown = """
            ---
            description: No name
            ---

            Body.
            """;

        Should.Throw<FormatException>(() => AgentMarkdownParser.Parse(markdown));
    }

    [Fact]
    public void Parse_MissingDescription_ThrowsFormatException()
    {
        var markdown = """
            ---
            name: no-desc
            ---

            Body.
            """;

        Should.Throw<FormatException>(() => AgentMarkdownParser.Parse(markdown));
    }

    [Fact]
    public void Parse_NoFrontmatter_ThrowsFormatException()
    {
        var markdown = "Just some text without frontmatter.";

        Should.Throw<FormatException>(() => AgentMarkdownParser.Parse(markdown));
    }

    [Fact]
    public void Parse_ToolsWithoutBrackets_ParsedCorrectly()
    {
        var markdown = """
            ---
            name: test
            description: test
            tools: web_search, git
            ---

            Body.
            """;

        var result = AgentMarkdownParser.Parse(markdown);

        result.Tools.Count.ShouldBe(2);
        result.Tools.ShouldContain("web_search");
        result.Tools.ShouldContain("git");
    }

    [Fact]
    public void Parse_SetsDirectoryPath()
    {
        var markdown = """
            ---
            name: test
            description: test
            ---

            Body.
            """;

        var result = AgentMarkdownParser.Parse(markdown, "/home/agents/test/AGENT.md");

        result.DirectoryPath.ShouldBe("/home/agents/test");
    }
}
