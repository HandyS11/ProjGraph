namespace DesignPatterns.Interfaces;

/// <summary>
/// Observer pattern: Represents a domain event
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
    string EventType { get; }
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent);
}
