using EventService.Application.DTOs;
using EventService.Domain.Entities;

namespace EventService.Application.Mapping;

/// <summary>
/// Manual mapping entity -> DTO. Kept explicit on purpose so the
/// projection is obvious and testable.
/// </summary>
internal static class VenueMappings
{
    public static VenueResponse ToResponse(this Venue venue) => new(
        venue.Id,
        venue.Name,
        venue.Address,
        venue.City,
        venue.Country,
        venue.Capacity,
        venue.CreatedAt,
        venue.UpdatedAt);
}
