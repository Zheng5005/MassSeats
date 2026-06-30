using EventService.Application.DTOs;

namespace EventService.Application.Interfaces;

public interface IEventService
{
    Task<EventResponse> CreateAsync(CreateEventRequest request, CancellationToken ct = default);
    Task<EventResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<EventResponse>> GetAllAsync(CancellationToken ct = default);
    Task<EventResponse> UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct = default);
    Task<EventResponse> UpdatePricingAsync(Guid id, UpdateEventPricingRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<CategoryResponse>> GetCategoriesAsync(CancellationToken ct = default);
}
