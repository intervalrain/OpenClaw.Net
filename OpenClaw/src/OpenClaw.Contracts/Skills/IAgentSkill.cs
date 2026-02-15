namespace OpenClaw.Contracts.Skills;

public interface IAgentSkill
{
    string Name { get; }
    string Description { get; }
    Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default);
}