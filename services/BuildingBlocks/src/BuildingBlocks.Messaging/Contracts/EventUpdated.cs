namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by EventService when an event's details change, so Booking
/// can keep its local reflection in sync.
/// </summary>
public sealed record EventUpdated(
    Guid EventId,
    string Title,
    DateTimeOffset EventDate,
    int TotalSeats) : IntegrationEvent;
