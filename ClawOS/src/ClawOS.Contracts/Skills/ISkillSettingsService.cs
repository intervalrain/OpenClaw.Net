using ClawOS.Contracts.Skills.Dtos;

namespace ClawOS.Contracts.Skills;

public interface IToolSettingsService
{
    Task<List<SkillSettingDto>> GetListAsync(CancellationToken ct = default);
    Task<bool> IsEnabledAsync(string skillName, CancellationToken ct = default);
    Task EnableAsync(string skillName, CancellationToken ct = default);
    Task DisableAsync(string skillName, CancellationToken ct = default);
    Task<List<IAgentTool>> GetEnabledSkillsAsync(CancellationToken ct = default);
}