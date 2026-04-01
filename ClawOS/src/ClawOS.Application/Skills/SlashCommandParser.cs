using System.Text.Json;
using System.Text.RegularExpressions;

using ClawOS.Contracts.Llm;
using ClawOS.Contracts.Skills;

namespace ClawOS.Application.Skills;

public record SlashCommand(string SkillName, string RawArguments);

public interface ISlashCommandParser
{
    bool TryParse(string input, out SlashCommand? command);
    string ConvertToJson(SlashCommand command, IAgentTool skill);
}

public partial class SlashCommandParser : ISlashCommandParser
{
    [GeneratedRegex(@"^/(\w+)(?:\s+(.*))?$", RegexOptions.Singleline)]
    private static partial Regex SlashCommandRegex();

    public bool TryParse(string input, out SlashCommand? command)
    {
        command = null;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var match = SlashCommandRegex().Match(input.Trim());
        if (!match.Success)
            return false;

        var skillName = match.Groups[1].Value;
        var arguments = match.Groups[2].Success ? match.Groups[2].Value.Trim() : string.Empty;

        command = new SlashCommand(skillName, arguments);
        return true;
    }

    public string ConvertToJson(SlashCommand command, IAgentTool skill)
    {
        if (string.IsNullOrWhiteSpace(command.RawArguments))
            return "{}";

        // If already JSON, return as-is
        if (command.RawArguments.StartsWith('{'))
            return command.RawArguments;

        // Parse the arguments: first non-key=value token is positional, rest are key=value pairs
        var (positionalArg, keyValueArgs) = ParseArgumentsWithPositional(command.RawArguments);

        // Get first required parameter name from skill for positional argument
        if (!string.IsNullOrEmpty(positionalArg) && skill.Parameters is ToolParameters toolParams)
        {
            // Use first required parameter, or first parameter if no required ones
            var firstParam = toolParams.Required?.FirstOrDefault()
                ?? toolParams.Properties?.Keys.FirstOrDefault();

            if (firstParam != null && !keyValueArgs.ContainsKey(firstParam))
            {
                keyValueArgs[firstParam] = positionalArg;
            }
        }

        if (keyValueArgs.Count > 0)
        {
            return JsonSerializer.Serialize(keyValueArgs);
        }

        // Fallback: use "input" as default parameter name
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["input"] = command.RawArguments
        });
    }

    [GeneratedRegex(@"(\w+)\s*[:=]\s*([^\s,]+|""[^""]*""|'[^']*')", RegexOptions.Compiled)]
    private static partial Regex KeyValueRegex();

    private static (string? positional, Dictionary<string, string> keyValue) ParseArgumentsWithPositional(string arguments)
    {
        var keyValueResult = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = KeyValueRegex().Matches(arguments);

        // Track which parts are key=value pairs
        var keyValueSpans = new List<(int Start, int End)>();
        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim('"', '\'');
            keyValueResult[key] = value;
            keyValueSpans.Add((match.Index, match.Index + match.Length));
        }

        // Find positional argument (text before first key=value, not part of any key=value)
        string? positional = null;
        var trimmedArgs = arguments.Trim();

        if (keyValueSpans.Count > 0)
        {
            // Get text before first key=value match
            var firstKeyValueStart = keyValueSpans.Min(s => s.Start);
            if (firstKeyValueStart > 0)
            {
                positional = trimmedArgs[..firstKeyValueStart].Trim();
            }
        }
        else
        {
            // No key=value pairs, entire string is positional
            positional = trimmedArgs;
        }

        return (positional, keyValueResult);
    }
}