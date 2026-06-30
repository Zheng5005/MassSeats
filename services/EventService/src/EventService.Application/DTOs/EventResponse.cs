namespace EventService.Application.DTOs;

/// <summary>
/// Outbound representation of an event.
/// </summary>
public sealed record EventResponse(
    Guid Id,
    string Title,
    string? Description,
    Guid CategoryId,
    Guid VenueId,
    DateTimeOffset EventDate,
    decimal TicketPrice,
    int TotalSeats,
    string? BannerImage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
