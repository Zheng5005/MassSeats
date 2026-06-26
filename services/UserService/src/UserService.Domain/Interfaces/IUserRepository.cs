using UserService.Domain.Entities;

namespace UserService.Domain.Interfaces;

/// <summary>
/// Persistence contract for the User aggregate. Defined in the domain
/// (the inner layer) and implemented in Infrastructure — this is the
/// dependency inversion that keeps the domain free of EF Core.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    void Update(User user);
    void Remove(User user);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
