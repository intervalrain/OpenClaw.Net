using OpenClaw.Domain.SkillStore.Entities;
using OpenClaw.Domain.SkillStore.Enums;

namespace OpenClaw.Domain.SkillStore.Repositories;

public interface ISkillListingRepository
{
    Task<SkillListing?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SkillListing?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<SkillListing>> GetAllAsync(SkillPublishStatus? status = null, string? category = null, string? search = null, int limit = 50, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<SkillListing>> GetByAuthorAsync(Guid authorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillListing>> GetPendingReviewAsync(CancellationToken ct = default);
    Task<int> GetCountAsync(SkillPublishStatus? status = null, CancellationToken ct = default);
    Task AddAsync(SkillListing entity, CancellationToken ct = default);
    Task UpdateAsync(SkillListing entity, CancellationToken ct = default);
    Task DeleteAsync(SkillListing entity, CancellationToken ct = default);
}
