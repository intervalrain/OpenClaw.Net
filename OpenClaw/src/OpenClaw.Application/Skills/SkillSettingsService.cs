using OpenClaw.Contracts.Skills;
using OpenClaw.Contracts.Skills.Dtos;
using OpenClaw.Domain.Skills.Entities;
using OpenClaw.Domain.Skills.Repositories;

using Weda.Core.Application.Interfaces;

namespace OpenClaw.Application.Skills;

public class SkillSettingsService(
    ISkillRegistry registry,
    ISkillSettingRepository repository,
    IUnitOfWork uow) : ISkillSettingsService
{
    public async Task<List<SkillSettingDto>> GetListAsync(CancellationToken ct = default)
    {
        var skills = registry.GetAllSkills();
        var settings = await repository.GetAllAsync(ct);
        var settingsDict = settings.ToDictionary(s => s.SkillName, StringComparer.OrdinalIgnoreCase);

        return skills.Select(skill =>
        {
            var isEnabled = !settingsDict.TryGetValue(skill.Name, out var setting) || setting.IsEnabled;
            return new SkillSettingDto(skill.Name, skill.Description, isEnabled);
        }).ToList();
    }

    public async Task<bool> IsEnabledAsync(string skillName, CancellationToken ct = default)
    {
        var setting = await repository.GetByNameAsync(skillName, ct);
        return setting?.IsEnabled ?? true;
    }
    
    public async Task EnableAsync(string skillName, CancellationToken ct = default)
    {
        await SetEnabledAsync(skillName, true, ct);
    }

    public async Task DisableAsync(string skillName, CancellationToken ct = default)
    {
        await SetEnabledAsync(skillName, false, ct);
    }

    public async Task<List<IAgentSkill>> GetEnabledSkillsAsync(CancellationToken ct = default)
    {
        var skills = registry.GetAllSkills();
        var settings = await repository.GetAllAsync(ct);
        var disabledSkills = settings
            .Where(s => !s.IsEnabled)
            .Select(s => s.SkillName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        return skills.Where(s => !disabledSkills.Contains(s.Name)).ToList();
    }

    private async Task SetEnabledAsync(string skillName, bool enable, CancellationToken ct)
    {
        var setting = await repository.GetByNameAsync(skillName, ct);
        if (setting == null)
        {
            setting = SkillSetting.Create(skillName, enable);
            await repository.AddAsync(setting, ct);
        }
        else
        {
            if (enable) setting.Enable();
            else setting.Disable();
            await repository.UpdateAsync(setting, ct);
        }

        await uow.SaveChangesAsync(ct);
    }

}