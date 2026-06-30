namespace EventService.Application.DTOs;

public sealed record UpdateEventRequest(
    string Title,
    string? Description,
    Guid CategoryId,
    Guid VenueId,
    DateTimeOffset EventDate);
