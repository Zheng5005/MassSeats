using BuildingBlocks.Domain;

namespace EventService.Domain.Events;

/// <summary>
/// Raised when an event is cancelled. Infrastructure translates it into
/// the EventCancelled integration event for interested services.
/// </summary>
public sealed record EventCancelledDomainEvent(Guid EventId) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
