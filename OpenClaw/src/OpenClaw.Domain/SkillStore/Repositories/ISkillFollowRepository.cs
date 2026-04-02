using OpenClaw.Domain.SkillStore.Entities;

namespace OpenClaw.Domain.SkillStore.Repositories;

public interface ISkillFollowRepository
{
    Task<SkillFollow?> GetAsync(Guid skillListingId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillFollow>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillFollow>> GetBySkillAsync(Guid skillListingId, CancellationToken ct = default);
    Task AddAsync(SkillFollow entity, CancellationToken ct = default);
    Task DeleteAsync(SkillFollow entity, CancellationToken ct = default);
}
