namespace BuildingBlocks.Domain;

/// <summary>
/// Marker for an in-process domain event raised by an aggregate.
/// These stay inside a single service (not to be confused with the
/// integration events in BuildingBlocks.Messaging that travel over
/// the message bus between services).
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
