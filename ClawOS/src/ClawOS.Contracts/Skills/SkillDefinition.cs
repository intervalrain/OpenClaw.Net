namespace ClawOS.Contracts.Skills;

/// <summary>
/// Parsed skill definition from a SKILL.md file.
/// Structure:
///   skills/{feature}/SKILL.md
///   skills/{feature}/reference/   (optional - reference docs loaded into instructions)
///   skills/{feature}/scripts/     (optional - executable scripts)
/// </summary>
public record SkillDefinition : ISkill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Instructions { get; init; }
    public required IReadOnlyList<string> Tools { get; init; }

    /// <summary>Directory path of this skill (parent of SKILL.md).</summary>
    public string? DirectoryPath { get; init; }

    /// <summary>Reference documents content (from reference/ subdirectory).</summary>
    public IReadOnlyList<SkillResource>? References { get; init; }

    /// <summary>Script file paths (from scripts/ subdirectory).</summary>
    public IReadOnlyList<SkillResource>? Scripts { get; init; }
}

/// <summary>
/// A resource file bundled with a skill.
/// </summary>
public record SkillResource(string FileName, string Content);
