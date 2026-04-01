using ClawOS.Contracts.Skills;

namespace ClawOS.Application.Skills;

public class ToolRegistry(IEnumerable<IAgentTool> skills) : IToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _skills = skills.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IAgentTool> GetAllSkills() => _skills.Values.ToList();

    public IAgentTool? GetSkill(string name) => _skills.GetValueOrDefault(name);

    public bool TryGetSkill(string name, out IAgentTool? skill) => _skills.TryGetValue(name, out skill);

    public TSkill? GetSkill<TSkill>() => (TSkill)_skills.FirstOrDefault(kvp => kvp.Value is TSkill).Value;

    public bool TryGetSkill<TSkill>(out TSkill? skill)
    {
        skill = GetSkill<TSkill>();
        return skill != null;
    }
}