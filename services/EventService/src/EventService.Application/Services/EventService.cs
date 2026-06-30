using EventService.Application.DTOs;
using EventService.Application.Interfaces;
using EventService.Application.Mapping;
using EventService.Domain.Entities;
using EventService.Domain.Exceptions;
using EventService.Domain.Interfaces;

namespace EventService.Application.Services;

/// <summary>
/// Classic application service that orchestrates the Event use cases.
/// It coordinates the domain entity and the repository — but holds no
/// business invariants itself (those live in the entity).
/// </summary>
public sealed class EventAppService : IEventService
{
    private readonly IEventRepository _repository;

    public EventAppService(IEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<EventResponse> CreateAsync(CreateEventRequest request, CancellationToken ct = default)
    {
        var title = request.Title.Trim();

        if (await _repository.TitleExistsAsync(title, ct))
            throw new DuplicateEventException(title);

        var @event = Event.Create(
            title: title,
            description: request.Description,
            categoryId: request.CategoryId,
            venueId: request.VenueId,
            eventDate: request.EventDate,
            ticketPrice: request.TicketPrice,
            totalSeats: request.TotalSeats,
            bannerImage: request.BannerImage);

        await _repository.AddAsync(@event, ct);
        await _repository.SaveChangesAsync(ct);

        return @event.ToResponse();
    }

    public async Task<EventResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var @event = await _repository.GetByIdAsync(id, ct);
        return @event?.ToResponse();
    }

    public async Task<List<EventResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var events = await _repository.GetAllAsync(ct);
        return events.Select(e => e.ToResponse()).ToList();
    }

    public async Task<EventResponse> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct = default)
    {
        var @event = await _repository.GetByIdAsync(id, ct)
                     ?? throw new EventNotFoundException(id);

        @event.UpdateDetails(
            request.Title,
            request.Description,
            request.CategoryId,
            request.VenueId,
            request.EventDate);

        _repository.Update(@event);
        await _repository.SaveChangesAsync(ct);

        return @event.ToResponse();
    }

    public async Task<EventResponse> UpdatePricingAsync(Guid id, UpdateEventPricingRequest request, CancellationToken ct = default)
    {
        var @event = await _repository.GetByIdAsync(id, ct)
                     ?? throw new EventNotFoundException(id);

        @event.UpdatePricing(request.TicketPrice);

        _repository.Update(@event);
        await _repository.SaveChangesAsync(ct);

        return @event.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var @event = await _repository.GetByIdAsync(id, ct)
                     ?? throw new EventNotFoundException(id);

        _repository.Remove(@event);
        await _repository.SaveChangesAsync(ct);
    }

    public async Task<List<CategoryResponse>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await _repository.GetCategoriesAsync(ct);
        return categories.Select(c => c.ToResponse()).ToList();
    }
}
