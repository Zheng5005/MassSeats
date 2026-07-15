using BuildingBlocks.Domain;

namespace BookingService.Domain.Events;

/// <summary>
/// Raised when a reservation is cancelled. Infrastructure translates it
/// into the <c>ReservationCancelled</c> integration event so Event can
/// release the seat back into availability.
/// </summary>
public sealed record ReservationCancelledDomainEvent(
    Guid ReservationId,
    Guid EventId,
    string SeatSection,
    string SeatRow,
    int SeatNumber) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
