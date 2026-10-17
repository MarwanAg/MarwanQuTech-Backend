namespace MarwanQuTech.Application.Common.Events;

/// <summary>
/// Wraps a framework-free domain event as a MediatR notification.
/// Infrastructure publishes one per event after SaveChanges succeeds:
/// <c>Activator.CreateInstance(typeof(DomainEventNotification&lt;&gt;).MakeGenericType(e.GetType()), e)</c>.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
