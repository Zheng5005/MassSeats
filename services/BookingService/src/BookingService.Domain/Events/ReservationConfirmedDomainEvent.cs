using BuildingBlocks.Domain;

namespace BookingService.Domain.Events;

/// <summary>
/// Raised when a reservation is confirmed after a successful payment.
/// Infrastructure translates it into the <c>ReservationConfirmed</c>
/// integration event.
/// </summary>
public sealed record ReservationConfirmedDomainEvent(
    Guid ReservationId,
    Guid EventId) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
