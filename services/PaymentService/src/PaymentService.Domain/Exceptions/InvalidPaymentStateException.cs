using BuildingBlocks.Domain;
using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on a payment whose current
/// state does not allow it (e.g. succeeding an already-succeeded payment).
/// The API layer maps this to a 409 Conflict.
/// </summary>
public sealed class InvalidPaymentStateException : DomainException
{
    public InvalidPaymentStateException(Guid id, PaymentStatus current, string attemptedAction)
        : base($"Cannot {attemptedAction} payment '{id}' while it is '{current}'.") { }
}
