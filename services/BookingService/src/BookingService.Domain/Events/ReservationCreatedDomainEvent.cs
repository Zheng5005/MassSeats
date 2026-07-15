using BuildingBlocks.Domain;

namespace BookingService.Domain.Events;

/// <summary>
/// Raised when a reservation is created (Pending). Infrastructure
/// translates this in-process domain event into the <c>SeatReserved</c>
/// integration event and writes it to the outbox.
/// </summary>
public sealed record ReservationCreatedDomainEvent(
    Guid ReservationId,
    Guid EventId,
    Guid UserId,
    string SeatSection,
    string SeatRow,
    int SeatNumber,
    decimal Amount) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
