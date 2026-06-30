using EventService.Domain.Entities;

namespace EventService.Domain.Interfaces;

/// <summary>
/// Persistence contract for the Event aggregate. Defined in the domain
/// (the inner layer) and implemented in Infrastructure — this is the
/// dependency inversion that keeps the domain free of EF Core.
/// </summary>
public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Event>> GetAllAsync(CancellationToken ct = default);
    Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default);
    Task<bool> TitleExistsAsync(string title, CancellationToken ct = default);
    Task AddAsync(Event @event, CancellationToken ct = default);
    void Update(Event @event);
    void Remove(Event @event);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
