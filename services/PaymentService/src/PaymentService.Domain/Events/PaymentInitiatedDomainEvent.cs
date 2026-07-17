using BuildingBlocks.Domain;

namespace PaymentService.Domain.Events;

/// <summary>
/// Raised when a payment is initiated (Pending). Infrastructure translates
/// this into the integration event so Booking can track the payment state.
/// </summary>
public sealed record PaymentInitiatedDomainEvent(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
