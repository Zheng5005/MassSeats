using BuildingBlocks.Domain;

namespace PaymentService.Domain.Events;

/// <summary>
/// Raised when a payment is initiated (Pending). This is an in-process
/// domain event only: there is no PaymentInitiated integration contract,
/// so it never leaves the service. Kept for internal auditing/hooks.
/// </summary>
public sealed record PaymentInitiatedDomainEvent(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
