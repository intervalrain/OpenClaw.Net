using OpenClaw.Domain.Users.Entities;

namespace OpenClaw.Domain.Users.Repositories;

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetByKeyAsync(Guid userId, string key, CancellationToken ct = default);
    Task<List<UserPreference>> GetAllByUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<UserPreference>> GetByPrefixAsync(Guid userId, string keyPrefix, CancellationToken ct = default);
    Task AddAsync(UserPreference preference, CancellationToken ct = default);
    Task UpdateAsync(UserPreference preference, CancellationToken ct = default);
    Task DeleteAsync(UserPreference preference, CancellationToken ct = default);
    Task DeleteByKeyAsync(Guid userId, string key, CancellationToken ct = default);
}
