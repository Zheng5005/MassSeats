using BookingService.Domain.Entities;

namespace BookingService.Domain.Interfaces;

/// <summary>
/// Persistence contract for the Reservation aggregate. Defined in the
/// domain (the inner layer) and implemented in Infrastructure — this is
/// the dependency inversion that keeps the domain free of EF Core.
/// </summary>
/// <remarks>
/// Seat uniqueness is NOT enforced here with a pre-check (that would race).
/// It is guaranteed by a unique constraint in the database on
/// (event_id, seat_section, seat_row, seat_number) for active reservations;
/// the repository implementation surfaces a violation as
/// <see cref="Exceptions.SeatAlreadyReservedException"/>.
/// </remarks>
public interface IReservationRepository
{
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns every reservation belonging to <paramref name="userId"/>,
    /// newest first. Used by the "My reservations" list endpoint.
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Pending reservations whose hold deadline has passed as of
    /// <paramref name="asOf"/>. Used by the expiration background worker.
    /// </summary>
    Task<IReadOnlyList<Reservation>> GetExpiredPendingAsync(DateTimeOffset asOf, CancellationToken ct = default);

    Task AddAsync(Reservation reservation, CancellationToken ct = default);
    void Update(Reservation reservation);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
