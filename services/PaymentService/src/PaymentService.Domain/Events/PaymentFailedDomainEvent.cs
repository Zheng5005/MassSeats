using BuildingBlocks.Domain;

namespace PaymentService.Domain.Events;

/// <summary>
/// Raised when a payment fails (Stripe declined or error). Infrastructure
/// translates this into the <c>PaymentFailed</c> integration event
/// so Booking can cancel the reservation.
/// </summary>
public sealed record PaymentFailedDomainEvent(
    Guid PaymentId,
    Guid BookingId) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
