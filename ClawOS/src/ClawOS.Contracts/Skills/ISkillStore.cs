namespace ClawOS.Contracts.Skills;

/// <summary>
/// Store for loading and managing Markdown-defined Skills.
/// </summary>
public interface ISkillStore
{
    IReadOnlyList<ISkill> GetAllSkills();
    ISkill? GetSkill(string name);
    Task ReloadAsync(CancellationToken ct = default);
}
