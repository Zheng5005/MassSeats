using EventService.Application.DTOs;
using EventService.Domain.Entities;

namespace EventService.Application.Mapping;

/// <summary>
/// Manual mapping entity -> DTO. Kept explicit on purpose so the
/// projection is obvious and testable.
/// </summary>
internal static class EventMappings
{
    public static EventResponse ToResponse(this Event @event) => new(
        @event.Id,
        @event.Title,
        @event.Description,
        @event.CategoryId,
        @event.VenueId,
        @event.EventDate,
        @event.TicketPrice,
        @event.TotalSeats,
        @event.BannerImage,
        @event.CreatedAt,
        @event.UpdatedAt);
}
