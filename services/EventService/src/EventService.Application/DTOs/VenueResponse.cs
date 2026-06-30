namespace EventService.Application.DTOs;

/// <summary>
/// Outbound representation of a venue.
/// </summary>
public sealed record VenueResponse(
    Guid Id,
    string Name,
    string Address,
    string City,
    string Country,
    int Capacity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
