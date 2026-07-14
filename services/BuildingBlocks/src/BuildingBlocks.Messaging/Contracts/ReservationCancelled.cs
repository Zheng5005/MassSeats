namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by BookingService when a reservation is cancelled, so
/// Event releases the seat back into availability.
/// </summary>
public sealed record ReservationCancelled(
    Guid ReservationId,
    Guid EventId,
    string SeatSection,
    string SeatRow,
    int SeatNumber) : IntegrationEvent;
