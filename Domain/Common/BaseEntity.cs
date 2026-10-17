namespace MarwanQuTech.Domain.Common;

/// <summary>
/// Root of every entity: identity + a buffer of domain events
/// that the Infrastructure layer dispatches after SaveChanges succeeds.
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public int Id { get; protected set; }

    /// <summary>Not persisted (ignored in EF configuration).</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
