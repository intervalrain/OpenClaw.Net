using OpenClaw.Contracts.Skills.Dtos;

namespace OpenClaw.Contracts.Skills;

public interface ISkillSettingsService
{
    Task<List<SkillSettingDto>> GetListAsync(CancellationToken ct = default);
    Task<bool> IsEnabledAsync(string skillName, CancellationToken ct = default);
    Task EnableAsync(string skillName, CancellationToken ct = default);
    Task DisableAsync(string skillName, CancellationToken ct = default);
    Task<List<IAgentSkill>> GetEnabledSkillsAsync(CancellationToken ct = default);
}