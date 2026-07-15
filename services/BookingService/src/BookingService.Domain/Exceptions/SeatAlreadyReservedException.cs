using BuildingBlocks.Domain;

namespace BookingService.Domain.Exceptions;

/// <summary>
/// Thrown when a seat is already held or confirmed for an event. In
/// practice this is enforced by a unique constraint in the database;
/// Infrastructure catches the constraint violation and surfaces this.
/// The API layer maps it to a 409 Conflict.
/// </summary>
public sealed class SeatAlreadyReservedException : DomainException
{
    public SeatAlreadyReservedException(Guid eventId, string seatSection, string seatRow, int seatNumber)
        : base($"Seat {seatSection}-{seatRow}-{seatNumber} for event '{eventId}' is already reserved.") { }
}
