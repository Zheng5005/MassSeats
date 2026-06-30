using EventService.Domain.Entities;
using EventService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.Persistence;

public sealed class VenueRepository : IVenueRepository
{
    private readonly EventDbContext _context;

    public VenueRepository(EventDbContext context) => _context = context;

    public Task<Venue?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Venues.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<List<Venue>> GetAllAsync(CancellationToken ct = default) =>
        _context.Venues.AsNoTracking().ToListAsync(ct);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct = default) =>
        _context.Venues.AnyAsync(v => v.Name == name, ct);

    public async Task AddAsync(Venue venue, CancellationToken ct = default) =>
        await _context.Venues.AddAsync(venue, ct);

    public void Update(Venue venue) => _context.Venues.Update(venue);

    public void Remove(Venue venue) => _context.Venues.Remove(venue);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
