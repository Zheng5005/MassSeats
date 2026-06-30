using EventService.Domain.Entities;
using EventService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.Persistence;

public sealed class EventRepository : IEventRepository
{
    private readonly EventDbContext _context;

    public EventRepository(EventDbContext context) => _context = context;

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Events.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<Event>> GetAllAsync(CancellationToken ct = default) =>
        _context.Events.AsNoTracking().ToListAsync(ct);

    public Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default) =>
        _context.Categories.AsNoTracking().ToListAsync(ct);

    public Task<bool> TitleExistsAsync(string title, CancellationToken ct = default) =>
        _context.Events.AnyAsync(e => e.Title == title, ct);

    public async Task AddAsync(Event @event, CancellationToken ct = default) =>
        await _context.Events.AddAsync(@event, ct);

    public void Update(Event @event) => _context.Events.Update(@event);

    public void Remove(Event @event) => _context.Events.Remove(@event);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
