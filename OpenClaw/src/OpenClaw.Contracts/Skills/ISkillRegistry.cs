namespace OpenClaw.Contracts.Skills;

public interface IToolRegistry
{
    IReadOnlyList<IAgentTool> GetAllSkills();
    IAgentTool? GetSkill(string name);
    bool TryGetSkill(string name, out IAgentTool? skill);
    TSkill? GetSkill<TSkill>();
    bool TryGetSkill<TSkill>(out TSkill? skill);
}