using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class StructuredOutputToolTests
{
    private readonly StructuredOutputTool _tool = new();

    [Fact]
    public async Task Validate_ValidJson_ShouldPass()
    {
        var ctx = new ToolContext("""
        {
            "json": "{\"name\": \"Alice\", \"age\": 30}",
            "schema": "{\"type\": \"object\", \"required\": [\"name\", \"age\"], \"properties\": {\"name\": {\"type\": \"string\"}, \"age\": {\"type\": \"integer\"}}}"
        }
        """);

        var result = await _tool.ExecuteAsync(ctx);
        result.IsSuccess.ShouldBeTrue();
        result.Output!.ShouldContain("PASSED");
    }

    [Fact]
    public async Task Validate_MissingRequired_ShouldFail()
    {
        var ctx = new ToolContext("""
        {
            "json": "{\"name\": \"Alice\"}",
            "schema": "{\"type\": \"object\", \"required\": [\"name\", \"age\"]}"
        }
        """);

        var result = await _tool.ExecuteAsync(ctx);
        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("missing required property 'age'");
    }

    [Fact]
    public async Task Validate_WrongType_ShouldFail()
    {
        var ctx = new ToolContext("""
        {
            "json": "{\"name\": 123}",
            "schema": "{\"type\": \"object\", \"properties\": {\"name\": {\"type\": \"string\"}}}"
        }
        """);

        var result = await _tool.ExecuteAsync(ctx);
        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("expected type 'string'");
    }

    [Fact]
    public async Task Validate_InvalidJson_ShouldFail()
    {
        var ctx = new ToolContext("""{"json": "not valid json{", "schema": "{}"}""");

        var result = await _tool.ExecuteAsync(ctx);
        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("Invalid JSON");
    }

    [Fact]
    public async Task Validate_ArrayItems_ShouldValidateEach()
    {
        var ctx = new ToolContext("""
        {
            "json": "[\"a\", 123]",
            "schema": "{\"type\": \"array\", \"items\": {\"type\": \"string\"}}"
        }
        """);

        var result = await _tool.ExecuteAsync(ctx);
        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("$[1]");
    }
}
