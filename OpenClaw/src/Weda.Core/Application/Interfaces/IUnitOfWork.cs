namespace Weda.Core.Application.Interfaces;

/// <summary>
/// Unit of Work pattern interface for managing transaction boundaries.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Saves all changes made in this unit of work to the database.
    /// </summary>
    /// <returns></returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}