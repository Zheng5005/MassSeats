using EventService.Domain.Entities;

namespace EventService.Domain.Interfaces;

/// <summary>
/// Persistence contract for the Venue aggregate. Defined in the domain
/// (the inner layer) and implemented in Infrastructure — this is the
/// dependency inversion that keeps the domain free of EF Core.
/// </summary>
public interface IEventRepository
{
    Task<Venue?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Venue>> GetAllAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);
    Task AddAsync(Venue venue, CancellationToken ct = default);
    void Update(Venue venue);
    void Remove(Venue venue);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
