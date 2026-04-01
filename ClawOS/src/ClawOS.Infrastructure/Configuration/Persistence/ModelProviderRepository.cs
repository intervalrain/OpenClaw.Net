using Microsoft.EntityFrameworkCore;

using ClawOS.Domain.Configuration.Entities;
using ClawOS.Domain.Configuration.Repositories;
using ClawOS.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace ClawOS.Infrastructure.Configuration.Persistence;

public class ModelProviderRepository(AppDbContext context)
     : GenericRepository<ModelProvider, Guid, AppDbContext>(context),
       IModelProviderRepository
{
    public async Task<ModelProvider?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ModelProvider>()
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);
    }

    public async Task<List<ModelProvider>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ModelProvider>()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }
}