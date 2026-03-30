using OpenClaw.Contracts.HierarchicalAgents;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.HierarchicalAgents;

/// <summary>
/// Parses AGENT.md files into AgentDefinition objects.
///
/// Format:
/// ---
/// name: script-writer
/// description: Generates video scripts from a topic
/// version: v1
/// type: llm
/// provider: openai
/// tools: [web_search, read_file]
/// ---
///
/// ## Instructions
/// (markdown body becomes the Instructions)
/// </summary>
public static class AgentMarkdownParser
{
    public static AgentDefinition Parse(string markdown, string? sourcePath = null)
    {
        var (frontmatter, body) = ExtractFrontmatter(markdown);

        var name = GetValue(frontmatter, "name")
            ?? throw new FormatException("AGENT.md must have 'name' in frontmatter");
        var description = GetValue(frontmatter, "description")
            ?? throw new FormatException("AGENT.md must have 'description' in frontmatter");

        var version = GetValue(frontmatter, "version") ?? "1.0";
        var typeStr = GetValue(frontmatter, "type") ?? "llm";
        var provider = GetValue(frontmatter, "provider");
        var toolsRaw = GetValue(frontmatter, "tools") ?? "";

        return new AgentDefinition
        {
            Name = name.Trim(),
            Description = description.Trim(),
            Version = version.Trim(),
            ExecutionType = ParseExecutionType(typeStr.Trim()),
            PreferredProvider = provider?.Trim(),
            Instructions = body.Trim(),
            Tools = ParseToolsList(toolsRaw),
            DirectoryPath = sourcePath is not null ? Path.GetDirectoryName(sourcePath) : null
        };
    }

    private static AgentExecutionType ParseExecutionType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "deterministic" => AgentExecutionType.Deterministic,
            "llm" => AgentExecutionType.Llm,
            "hybrid" => AgentExecutionType.Hybrid,
            _ => AgentExecutionType.Llm
        };
    }

    private static (Dictionary<string, string> frontmatter, string body) ExtractFrontmatter(string markdown)
    {
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = markdown.Split('\n');

        if (lines.Length == 0 || lines[0].Trim() != "---")
            return (frontmatter, markdown);

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

    private static string? GetValue(Dictionary<string, string> frontmatter, string key)
    {
        return frontmatter.TryGetValue(key, out var value) ? value : null;
    }

    private static List<string> ParseToolsList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var cleaned = raw.Trim().TrimStart('[').TrimEnd(']');
        return cleaned.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
