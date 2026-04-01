using Microsoft.Extensions.Logging;
using ClawOS.Contracts.Skills;

namespace ClawOS.Application.Skills;

/// <summary>
/// Loads Skills from SKILL.md files in a directory.
/// Expected structure:
///   skills/{feature}/SKILL.md
///   skills/{feature}/reference/   (optional - reference docs)
///   skills/{feature}/scripts/     (optional - scripts)
/// </summary>
public class FileSkillStore(string skillsDirectory, ILogger<FileSkillStore> logger) : ISkillStore
{
    private List<SkillDefinition> _skills = [];

    public IReadOnlyList<ISkill> GetAllSkills() => _skills;

    public ISkill? GetSkill(string name)
        => _skills.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var newSkills = new List<SkillDefinition>();

        if (!Directory.Exists(skillsDirectory))
        {
            logger.LogWarning("Skills directory not found: {Dir}", skillsDirectory);
            _skills = newSkills;
            return;
        }

        var files = Directory.GetFiles(skillsDirectory, "SKILL.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var skillDir = Path.GetDirectoryName(file)!;
                var skill = SkillMarkdownParser.Parse(content, file);

                // Load reference docs
                var references = await LoadResourcesAsync(Path.Combine(skillDir, "reference"), ct);

                // Load scripts
                var scripts = await LoadResourcesAsync(Path.Combine(skillDir, "scripts"), ct);

                // Append reference content to instructions if any
                var instructions = skill.Instructions;
                if (references.Count > 0)
                {
                    var refContent = string.Join("\n\n", references.Select(r =>
                        $"## Reference: {r.FileName}\n{r.Content}"));
                    instructions += $"\n\n---\n\n{refContent}";
                }

                newSkills.Add(skill with
                {
                    DirectoryPath = skillDir,
                    Instructions = instructions,
                    References = references,
                    Scripts = scripts
                });

                logger.LogInformation("Loaded skill: {Name} ({RefCount} refs, {ScriptCount} scripts) from {Path}",
                    skill.Name, references.Count, scripts.Count, file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse skill from {Path}", file);
            }
        }

        _skills = newSkills;
        logger.LogInformation("Loaded {Count} skills from {Dir}", newSkills.Count, skillsDirectory);
    }

    private static async Task<List<SkillResource>> LoadResourcesAsync(string dir, CancellationToken ct)
    {
        var resources = new List<SkillResource>();
        if (!Directory.Exists(dir)) return resources;

        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                var relativeName = Path.GetRelativePath(dir, file);
                resources.Add(new SkillResource(relativeName, content));
            }
            catch
            {
                // Skip unreadable files
            }
        }

        return resources;
    }
}
