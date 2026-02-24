namespace OpenClaw.Contracts.Skills;

public interface ISkillRegistry
{
    IReadOnlyList<IAgentSkill> GetAllSkills();
    IAgentSkill? GetSkill(string name);
    bool TryGetSkill(string name, out IAgentSkill? skill);
}