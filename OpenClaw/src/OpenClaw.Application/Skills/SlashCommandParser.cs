using System.Text.Json;
using System.Text.RegularExpressions;

using OpenClaw.Contracts.Llm;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Skills;

public record SlashCommand(string SkillName, string RawArguments);

public interface ISlashCommandParser
{
    bool TryParse(string input, out SlashCommand? command);
    string ConvertToJson(SlashCommand command, IAgentSkill skill);
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

    public string ConvertToJson(SlashCommand command, IAgentSkill skill)
    {
        if (string.IsNullOrWhiteSpace(command.RawArguments))
            return "{}";

        // If already JSON, return as-is
        if (command.RawArguments.StartsWith('{'))
            return command.RawArguments;

        // Try to parse key:value or key=value pairs
        var keyValueArgs = ParseKeyValueArguments(command.RawArguments);
        if (keyValueArgs.Count > 0)
        {
            return JsonSerializer.Serialize(keyValueArgs);
        }

        // Get first parameter name from skill
        if (skill.Parameters is ToolParameters toolParams && toolParams.Properties?.Count > 0)
        {
            var firstParam = toolParams.Properties.Keys.First();
            return JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [firstParam] = command.RawArguments
            });
        }

        // Fallback: use "input" as default parameter name
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["input"] = command.RawArguments
        });
    }

    [GeneratedRegex(@"(\w+)\s*[:=]\s*([^\s,]+|""[^""]*""|'[^']*')", RegexOptions.Compiled)]
    private static partial Regex KeyValueRegex();

    private static Dictionary<string, string> ParseKeyValueArguments(string arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = KeyValueRegex().Matches(arguments);

        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim('"', '\'');
            result[key] = value;
        }

        return result;
    }
}