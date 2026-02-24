using Microsoft.EntityFrameworkCore;

using OpenClaw.Domain.Skills.Entities;
using OpenClaw.Domain.Skills.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Skills.Persistence;

public class SkillSettingRepository(AppDbContext context)
    : GenericRepository<SkillSetting, Guid, AppDbContext>(context), ISkillSettingRepository
{
    public async Task<SkillSetting?> GetByNameAsync(string skillName, CancellationToken ct = default)
    {
        return await DbContext.Set<SkillSetting>()
            .FirstOrDefaultAsync(x => x.SkillName == skillName, ct);
    }
}