using OpenClaw.Domain.Configuration.Entities;
using Weda.Core.Domain;

namespace OpenClaw.Domain.Configuration.Repositories;

public interface IModelProviderRepository : IRepository<ModelProvider, Guid>
{
    Task<ModelProvider?> GetActiveAsync(CancellationToken cancellationToken = default);
}