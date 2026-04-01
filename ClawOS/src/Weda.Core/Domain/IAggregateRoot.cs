namespace Weda.Core.Domain;

public interface IAggregateRoot
{
    List<IDomainEvent> PopDomainEvents();
}
