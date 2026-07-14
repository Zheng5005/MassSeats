namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by BookingService when a seat is tentatively reserved
/// (Pending). Payment starts the charge; Event decrements its
/// informational available-seats reflection.
/// </summary>
public sealed record SeatReserved(
    Guid ReservationId,
    Guid EventId,
    Guid UserId,
    string SeatSection,
    string SeatRow,
    int SeatNumber,
    decimal Amount) : IntegrationEvent;
