using EventService.Application.DTOs;
using EventService.Application.Interfaces;
using EventService.Application.Mapping;
using EventService.Domain.Entities;
using EventService.Domain.Exceptions;
using EventService.Domain.Interfaces;

namespace EventService.Application.Services;

/// <summary>
/// Classic application service that orchestrates the Venue use cases.
/// It coordinates the domain entity and the repository — but holds no
/// business invariants itself (those live in the entity).
/// </summary>
public sealed class VenueAppService : IVenueService
{
    private readonly IVenueRepository _repository;

    public VenueAppService(IVenueRepository repository)
    {
        _repository = repository;
    }

    public async Task<VenueResponse> CreateAsync(CreateVenueRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();

        if (await _repository.NameExistsAsync(name, ct))
            throw new DuplicateVenueException(name);

        var venue = Venue.Create(
            name: name,
            address: request.Address,
            city: request.City,
            country: request.Country,
            capacity: request.Capacity);

        await _repository.AddAsync(venue, ct);
        await _repository.SaveChangesAsync(ct);

        return venue.ToResponse();
    }

    public async Task<VenueResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var venue = await _repository.GetByIdAsync(id, ct);
        return venue?.ToResponse();
    }

    public async Task<List<VenueResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var venues = await _repository.GetAllAsync(ct);
        return venues.Select(v => v.ToResponse()).ToList();
    }

    public async Task<VenueResponse> UpdateAsync(Guid id, UpdateVenueRequest request, CancellationToken ct = default)
    {
        var venue = await _repository.GetByIdAsync(id, ct)
                    ?? throw new VenueNotFoundException(id);

        venue.Update(
            request.Name,
            request.Address,
            request.City,
            request.Country,
            request.Capacity);

        _repository.Update(venue);
        await _repository.SaveChangesAsync(ct);

        return venue.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var venue = await _repository.GetByIdAsync(id, ct)
                    ?? throw new VenueNotFoundException(id);

        _repository.Remove(venue);
        await _repository.SaveChangesAsync(ct);
    }
}
