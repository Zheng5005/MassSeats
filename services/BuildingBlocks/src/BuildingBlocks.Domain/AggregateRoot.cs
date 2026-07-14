namespace BuildingBlocks.Domain;

/// <summary>
/// Base class for aggregate roots. Adds a collection of domain events
/// that the aggregate raises while mutating state; the infrastructure
/// layer drains and dispatches them after persistence (e.g. into the
/// outbox), then calls <see cref="ClearDomainEvents"/>.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(Guid id) : base(id) { }

    protected void RaiseDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
