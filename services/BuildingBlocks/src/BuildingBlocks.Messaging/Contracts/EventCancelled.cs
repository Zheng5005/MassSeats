namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by EventService when an event is cancelled, so Booking can
/// cancel any reservations associated with it.
/// </summary>
public sealed record EventCancelled(
    Guid EventId) : IntegrationEvent;
