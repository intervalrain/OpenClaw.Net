using OpenClaw.Domain.SkillStore.Entities;

namespace OpenClaw.Domain.SkillStore.Repositories;

public interface ISkillStarRepository
{
    Task<SkillStar?> GetAsync(Guid skillListingId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillStar>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(SkillStar entity, CancellationToken ct = default);
    Task DeleteAsync(SkillStar entity, CancellationToken ct = default);
}
