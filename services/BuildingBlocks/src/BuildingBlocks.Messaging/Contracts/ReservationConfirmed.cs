namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by BookingService when a reservation is confirmed (after a
/// successful payment), so Event can confirm the seat occupation.
/// </summary>
public sealed record ReservationConfirmed(
    Guid ReservationId,
    Guid EventId) : IntegrationEvent;
