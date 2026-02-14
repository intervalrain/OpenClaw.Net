using Weda.Core.Domain;
using OpenClaw.Domain.Users.Entities;

namespace OpenClaw.Domain.Users.Repositories;

public interface IUserRepository : IRepository<User, Guid>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);
}
