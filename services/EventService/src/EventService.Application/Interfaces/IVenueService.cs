using EventService.Application.DTOs;

namespace EventService.Application.Interfaces;

public interface IVenueService
{
    Task<VenueResponse> CreateAsync(CreateVenueRequest request, CancellationToken ct = default);
    Task<VenueResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<VenueResponse>> GetAllAsync(CancellationToken ct = default);
    Task<VenueResponse> UpdateAsync(Guid id, UpdateVenueRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
