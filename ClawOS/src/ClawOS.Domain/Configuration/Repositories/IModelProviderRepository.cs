using ClawOS.Domain.Configuration.Entities;
using Weda.Core.Domain;

namespace ClawOS.Domain.Configuration.Repositories;

public interface IModelProviderRepository : IRepository<ModelProvider, Guid>
{
    Task<ModelProvider?> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<List<ModelProvider>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}