namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by BookingService when a Pending reservation times out, so
/// Event releases the seat. Raised by the expiration background worker.
/// </summary>
public sealed record ReservationExpired(
    Guid ReservationId,
    Guid EventId,
    string SeatSection,
    string SeatRow,
    int SeatNumber) : IntegrationEvent;
