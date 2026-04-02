using OpenClaw.Domain.SkillStore.Entities;

namespace OpenClaw.Domain.SkillStore.Repositories;

public interface ISkillInstallationRepository
{
    Task<SkillInstallation?> GetAsync(Guid skillListingId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillInstallation>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillInstallation>> GetBySkillAsync(Guid skillListingId, CancellationToken ct = default);
    Task<IReadOnlyList<SkillInstallation>> GetWithUpdatesAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(SkillInstallation entity, CancellationToken ct = default);
    Task UpdateAsync(SkillInstallation entity, CancellationToken ct = default);
    Task DeleteAsync(SkillInstallation entity, CancellationToken ct = default);
}
