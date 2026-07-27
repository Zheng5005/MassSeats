using BuildingBlocks.Domain;

namespace EventService.Domain.Events;

/// <summary>
/// Raised when event details change. Infrastructure translates it into
/// the EventUpdated integration event for interested services.
/// </summary>
public sealed record EventUpdatedDomainEvent(
    Guid EventId,
    string Title,
    DateTimeOffset EventDate,
    int TotalSeats) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
