using OpenClaw.Domain.Configuration.Entities;

using Weda.Core.Domain;

namespace OpenClaw.Domain.Configuration.Repositories;

public interface IAppConfigRepository : IRepository<AppConfig, Guid>
{
    Task<AppConfig?> GetByKeyAsync(string key, CancellationToken ct = default);
}