using BuildingBlocks.Domain;

namespace BookingService.Domain.Events;

/// <summary>
/// Raised when a Pending reservation times out. Infrastructure
/// translates it into the <c>ReservationExpired</c> integration event
/// so Event releases the seat.
/// </summary>
public sealed record ReservationExpiredDomainEvent(
    Guid ReservationId,
    Guid EventId,
    string SeatSection,
    string SeatRow,
    int SeatNumber) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
