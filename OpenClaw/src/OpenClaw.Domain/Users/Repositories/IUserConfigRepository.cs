using OpenClaw.Domain.Users.Entities;

namespace OpenClaw.Domain.Users.Repositories;

public interface IUserConfigRepository
{
    Task<UserConfig?> GetByKeyAsync(Guid userId, string key, CancellationToken ct = default);
    Task<IReadOnlyList<UserConfig>> GetAllByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(UserConfig config, CancellationToken ct = default);
    Task UpdateAsync(UserConfig config, CancellationToken ct = default);
    Task DeleteAsync(UserConfig config, CancellationToken ct = default);
}
