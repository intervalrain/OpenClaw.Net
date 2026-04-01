using ClawOS.Contracts.Skills;

namespace ClawOS.Application.Skills;

/// <summary>
/// Parses SKILL.md files into SkillDefinition objects.
///
/// Format:
/// ---
/// name: daily-ado-report
/// description: Generate daily work item report
/// tools: [azure_devops, git]
/// ---
///
/// ## Instructions
/// (markdown body becomes the Instructions)
/// </summary>
public static class SkillMarkdownParser
{
    public static SkillDefinition Parse(string markdown, string? sourcePath = null)
    {
        var (frontmatter, body) = ExtractFrontmatter(markdown);

        var name = GetFrontmatterValue(frontmatter, "name")
            ?? throw new FormatException("Skill markdown must have 'name' in frontmatter");
        var description = GetFrontmatterValue(frontmatter, "description")
            ?? throw new FormatException("Skill markdown must have 'description' in frontmatter");
        var toolsRaw = GetFrontmatterValue(frontmatter, "tools") ?? "";

        var tools = ParseToolsList(toolsRaw);

        return new SkillDefinition
        {
            Name = name.Trim(),
            Description = description.Trim(),
            Instructions = body.Trim(),
            Tools = tools,
            DirectoryPath = sourcePath != null ? Path.GetDirectoryName(sourcePath) : null
        };
    }

    private static (Dictionary<string, string> frontmatter, string body) ExtractFrontmatter(string markdown)
    {
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = markdown.Split('\n');

        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return (frontmatter, markdown);
        }

        var bodyStart = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                bodyStart = i + 1;
                break;
            }

            var colonIdx = lines[i].IndexOf(':');
            if (colonIdx > 0)
            {
                var key = lines[i][..colonIdx].Trim();
                var value = lines[i][(colonIdx + 1)..].Trim();
                frontmatter[key] = value;
            }
        }

        var body = bodyStart >= 0 && bodyStart < lines.Length
            ? string.Join('\n', lines[bodyStart..])
            : "";

        return (frontmatter, body);
    }

    private static string? GetFrontmatterValue(Dictionary<string, string> frontmatter, string key)
    {
        return frontmatter.TryGetValue(key, out var value) ? value : null;
    }

    private static List<string> ParseToolsList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        // Support both [tool1, tool2] and tool1, tool2
        var cleaned = raw.Trim().TrimStart('[').TrimEnd(']');
        return cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
