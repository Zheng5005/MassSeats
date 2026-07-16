namespace BookingService.Application.DTOs;

/// <summary>
/// Outbound representation of a reservation.
/// </summary>
public sealed record ReservationResponse(
    Guid Id,
    Guid UserId,
    Guid EventId,
    string SeatSection,
    string SeatRow,
    int SeatNumber,
    decimal Price,
    string Status,
    Guid? PaymentId,
    DateTimeOffset ReservedAt,
    DateTimeOffset ExpiresAt);
