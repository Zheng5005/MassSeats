using BuildingBlocks.Domain;

namespace PaymentService.Domain.Exceptions;

/// <summary>
/// A payment for the same booking was already persisted — a concurrent
/// create raced and lost. The caller should return the existing payment
/// instead of surfacing the raw unique-violation as a 500.
/// </summary>
public sealed class DuplicatePaymentException : DomainException
{
    public DuplicatePaymentException(Guid bookingId)
        : base($"A payment for booking '{bookingId}' already exists.") { }
}
