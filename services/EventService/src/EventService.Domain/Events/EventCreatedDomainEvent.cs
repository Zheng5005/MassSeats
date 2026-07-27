using BuildingBlocks.Domain;

namespace EventService.Domain.Events;

/// <summary>
/// Raised when an event is created. Infrastructure translates it into
/// the EventCreated integration event for interested services.
/// </summary>
public sealed record EventCreatedDomainEvent(
    Guid EventId,
    string Title,
    Guid VenueId,
    Guid CategoryId,
    DateTimeOffset EventDate,
    int TotalSeats) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
