namespace Weda.Core.Domain;

/// <summary>
/// Marker interface for entities that belong to a specific user.
/// Used by EF Core global query filters to automatically scope queries.
/// </summary>
public interface IUserScoped
{
    Guid GetOwnerUserId();
}
