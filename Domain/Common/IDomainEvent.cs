namespace MarwanQuTech.Domain.Common;

/// <summary>
/// Marker for something meaningful that happened in the domain.
/// Kept framework-free: the Application layer wraps these in MediatR notifications.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent(DateTime OccurredOnUtc) : IDomainEvent;
