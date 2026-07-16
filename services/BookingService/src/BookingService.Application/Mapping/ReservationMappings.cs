using BookingService.Application.DTOs;
using BookingService.Domain.Entities;

namespace BookingService.Application.Mapping;

/// <summary>
/// Manual mapping entity -> DTO. Kept explicit on purpose so the
/// projection is obvious and testable.
/// </summary>
internal static class ReservationMappings
{
    public static ReservationResponse ToResponse(this Reservation reservation) => new(
        reservation.Id,
        reservation.UserId,
        reservation.EventId,
        reservation.SeatSection,
        reservation.SeatRow,
        reservation.SeatNumber,
        reservation.Price,
        reservation.Status.ToString(),
        reservation.PaymentId,
        reservation.ReservedAt,
        reservation.ExpiresAt);
}
