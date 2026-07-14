namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by EventService when a new event is created, so Booking
/// learns the event exists along with its capacity/layout basics.
/// </summary>
public sealed record EventCreated(
    Guid EventId,
    string Title,
    Guid VenueId,
    Guid CategoryId,
    DateTimeOffset EventDate,
    int TotalSeats) : IntegrationEvent;
