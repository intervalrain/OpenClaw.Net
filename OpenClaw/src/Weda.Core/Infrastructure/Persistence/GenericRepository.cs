using Microsoft.EntityFrameworkCore;
using Weda.Core.Domain;

namespace Weda.Core.Infrastructure.Persistence;

public class GenericRepository<T, TId, TDbContext>(TDbContext dbContext) : IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
    where TDbContext : DbContext
{
    protected readonly TDbContext DbContext = dbContext;
    protected readonly DbSet<T> DbSet = dbContext.Set<T>();

    public virtual async Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);
    }

    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }
}
