using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.Skills;

public class SkillRegistry(IEnumerable<IAgentSkill> skills) : ISkillRegistry
{
    private readonly Dictionary<string, IAgentSkill> _skills = skills.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IAgentSkill> GetAllSkills() => _skills.Values.ToList();

    public IAgentSkill? GetSkill(string name) => _skills.GetValueOrDefault(name);

    public bool TryGetSkill(string name, out IAgentSkill? skill) => _skills.TryGetValue(name, out skill);
}