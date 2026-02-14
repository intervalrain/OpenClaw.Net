namespace Weda.Core.Domain;

public abstract class Entity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id)
    {
        Id = id;
    }

    protected Entity()
    {
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity<TId> other && Id.Equals(other.Id);
    }

    public override int GetHashCode() => Id.GetHashCode();
}
