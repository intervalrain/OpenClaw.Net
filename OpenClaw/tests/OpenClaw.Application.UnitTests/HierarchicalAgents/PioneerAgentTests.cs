using OpenClaw.Application.HierarchicalAgents;
using Shouldly;

namespace OpenClaw.Application.UnitTests.HierarchicalAgents;

public class PioneerAgentTests
{
    [Fact]
    public void ExtractJson_DirectJson_ReturnsAsIs()
    {
        var json = """{"name":"test","nodes":[],"edges":[]}""";

        var result = PioneerAgent.ExtractJson(json);

        result.ShouldBe(json);
    }

    [Fact]
    public void ExtractJson_MarkdownFenced_ExtractsJson()
    {
        var content = """
            Here is the plan:
            ```json
            {"name":"test","nodes":[],"edges":[]}
            ```
            """;

        var result = PioneerAgent.ExtractJson(content);

        result.ShouldNotBeNull();
        result.ShouldContain("\"name\":\"test\"");
    }

    [Fact]
    public void ExtractJson_FencedWithoutLanguage_ExtractsJson()
    {
        var content = """
            ```
            {"name":"test","nodes":[],"edges":[]}
            ```
            """;

        var result = PioneerAgent.ExtractJson(content);

        result.ShouldNotBeNull();
        result.ShouldContain("\"name\":\"test\"");
    }

    [Fact]
    public void ExtractJson_NoJson_ReturnsNull()
    {
        var content = "This is just plain text without any JSON.";

        var result = PioneerAgent.ExtractJson(content);

        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractJson_WhitespaceAroundJson_Trims()
    {
        var content = "  \n  {\"name\":\"test\"}  \n  ";

        var result = PioneerAgent.ExtractJson(content);

        result.ShouldBe("{\"name\":\"test\"}");
    }

    [Fact]
    public void BuildSystemPrompt_ContainsAgentRegistry()
    {
        var agentDump = "- **read_file** (Deterministic): Reads files";

        var prompt = PioneerAgent.BuildSystemPrompt(agentDump);

        prompt.ShouldContain("read_file");
        prompt.ShouldContain("Reads files");
        prompt.ShouldContain("task planner");
        prompt.ShouldContain("Available Agents");
    }

    [Fact]
    public void BuildSystemPrompt_ContainsOutputFormat()
    {
        var prompt = PioneerAgent.BuildSystemPrompt("No agents available.");

        prompt.ShouldContain("\"nodes\"");
        prompt.ShouldContain("\"edges\"");
        prompt.ShouldContain("\"mapping\"");
    }

    [Fact]
    public void BuildSystemPrompt_ContainsRules()
    {
        var prompt = PioneerAgent.BuildSystemPrompt("No agents.");

        prompt.ShouldContain("MINIMUM");
        prompt.ShouldContain("deterministic");
        prompt.ShouldContain("JSONPath");
    }

    [Fact]
    public void Name_IsPioneer()
    {
        var agent = new PioneerAgent();

        agent.Name.ShouldBe("pioneer");
        agent.ExecutionType.ShouldBe(OpenClaw.Contracts.HierarchicalAgents.AgentExecutionType.Llm);
    }
}
