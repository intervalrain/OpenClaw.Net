using ClawOS.Domain.Skills.Entities;

using Weda.Core.Domain;

namespace ClawOS.Domain.Skills.Repositories;

public interface ISkillSettingRepository : IRepository<SkillSetting, Guid>
{
    Task<SkillSetting?> GetByNameAsync(string skillName, CancellationToken ct = default);
}