namespace EventService.Application.DTOs;

public sealed record CreateEventRequest(
    string Title,
    string? Description,
    Guid CategoryId,
    Guid VenueId,
    DateTimeOffset EventDate,
    decimal TicketPrice,
    int TotalSeats,
    string? BannerImage);
