using BuildingBlocks.Domain;

namespace PaymentService.Domain.Events;

/// <summary>
/// Raised when a payment succeeds (Stripe confirmed). Infrastructure
/// translates this into the <c>PaymentSucceeded</c> integration event
/// so Booking can confirm the reservation.
/// </summary>
public sealed record PaymentSucceededDomainEvent(
    Guid PaymentId,
    Guid BookingId) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
