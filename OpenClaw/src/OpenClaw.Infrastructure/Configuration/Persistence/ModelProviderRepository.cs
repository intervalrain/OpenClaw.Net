using Microsoft.EntityFrameworkCore;

using OpenClaw.Domain.Configuration.Entities;
using OpenClaw.Domain.Configuration.Repositories;
using OpenClaw.Infrastructure.Common.Persistence;

using Weda.Core.Infrastructure.Persistence;

namespace OpenClaw.Infrastructure.Configuration.Persistence;

public class ModelProviderRepository(AppDbContext context)
     : GenericRepository<ModelProvider, Guid, AppDbContext>(context),
       IModelProviderRepository
{
    public async Task<ModelProvider?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ModelProvider>()
            .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);
    }
}