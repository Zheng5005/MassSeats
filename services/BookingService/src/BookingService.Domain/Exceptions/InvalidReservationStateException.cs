using BuildingBlocks.Domain;
using BookingService.Domain.Enums;

namespace BookingService.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on a reservation whose current
/// state does not allow it (e.g. confirming an expired reservation).
/// The API layer maps this to a 409 Conflict.
/// </summary>
public sealed class InvalidReservationStateException : DomainException
{
    public InvalidReservationStateException(Guid id, ReservationStatus current, string attemptedAction)
        : base($"Cannot {attemptedAction} reservation '{id}' while it is '{current}'.") { }
}
