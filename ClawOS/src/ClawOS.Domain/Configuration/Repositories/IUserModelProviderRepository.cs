using ClawOS.Domain.Configuration.Entities;
using Weda.Core.Domain;

namespace ClawOS.Domain.Configuration.Repositories;

public interface IUserModelProviderRepository : IRepository<UserModelProvider, Guid>
{
    Task<List<UserModelProvider>> GetAllByUserAsync(Guid userId, CancellationToken ct = default);
    Task<UserModelProvider?> GetByNameAsync(Guid userId, string name, CancellationToken ct = default);
    Task<UserModelProvider?> GetDefaultAsync(Guid userId, CancellationToken ct = default);
}
