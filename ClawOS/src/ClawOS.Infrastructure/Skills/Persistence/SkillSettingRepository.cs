using Microsoft.EntityFrameworkCore;

using ClawOS.Domain.Skills.Entities;
using ClawOS.Domain.Skills.Repositories;
using ClawOS.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace ClawOS.Infrastructure.Skills.Persistence;

public class SkillSettingRepository(AppDbContext context)
    : GenericRepository<SkillSetting, Guid, AppDbContext>(context), ISkillSettingRepository
{
    public async Task<SkillSetting?> GetByNameAsync(string skillName, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillSetting>()
            .FirstOrDefaultAsync(x => x.SkillName == skillName, ct);
    }
}