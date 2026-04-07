using System.Text.RegularExpressions;

namespace OpenClaw.Application.Skills;

/// <summary>
/// Parses enhanced chat syntax:
///   //agentName args   → AgentInvoke (mount agent with system prompt)
///   /toolName args     → ToolInvoke (add tool to context, pass to LLM)
///   @path/to/file      → FileReference (inline, inject file content)
/// </summary>
public interface IChatSyntaxParser
{
    ChatSyntaxResult Parse(string input);
}

public abstract record ChatSyntaxResult;
public record AgentInvokeResult(string AgentName, string RawArguments) : ChatSyntaxResult;
public record ToolInvokeResult(string ToolName, string RawArguments) : ChatSyntaxResult;
public record PlainMessageResult(string Message, List<string> FileReferences) : ChatSyntaxResult;

public partial class ChatSyntaxParser : IChatSyntaxParser
{
    // //agentName [args]
    [GeneratedRegex(@"^//(\w+)(?:\s+(.*))?$", RegexOptions.Singleline)]
    private static partial Regex AgentInvokeRegex();

    // /toolName [args]
    [GeneratedRegex(@"^/(\w+)(?:\s+(.*))?$", RegexOptions.Singleline)]
    private static partial Regex ToolInvokeRegex();

    // @path/to/file (inline reference, must have extension)
    [GeneratedRegex(@"@([\w./\-]+\.\w+)")]
    private static partial Regex FileReferenceRegex();

    public ChatSyntaxResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new PlainMessageResult(input, []);

        var trimmed = input.Trim();

        // 1. Check for // agent invoke (must be before / check)
        var agentMatch = AgentInvokeRegex().Match(trimmed);
        if (agentMatch.Success)
        {
            var agentName = agentMatch.Groups[1].Value;
            var args = agentMatch.Groups[2].Success ? agentMatch.Groups[2].Value.Trim() : "";
            return new AgentInvokeResult(agentName, args);
        }

        // 2. Check for / tool invoke
        var toolMatch = ToolInvokeRegex().Match(trimmed);
        if (toolMatch.Success)
        {
            var toolName = toolMatch.Groups[1].Value;
            var args = toolMatch.Groups[2].Success ? toolMatch.Groups[2].Value.Trim() : "";
            return new ToolInvokeResult(toolName, args);
        }

        // 3. Plain message — extract inline @file references
        var fileRefs = FileReferenceRegex().Matches(trimmed)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        return new PlainMessageResult(trimmed, fileRefs);
    }
}
