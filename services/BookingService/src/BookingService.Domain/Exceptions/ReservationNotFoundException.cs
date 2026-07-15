using BuildingBlocks.Domain;

namespace BookingService.Domain.Exceptions;

public sealed class ReservationNotFoundException : DomainException
{
    public ReservationNotFoundException(Guid id)
        : base($"Reservation with id '{id}' was not found.") { }
}
