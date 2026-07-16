using BookingService.Domain.Entities;
using BookingService.Domain.Enums;
using BookingService.Domain.Exceptions;
using BookingService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingService.Infrastructure.Persistence;

public sealed class ReservationRepository : IReservationRepository
{
    private readonly BookingDbContext _context;

    public ReservationRepository(BookingDbContext context) => _context = context;

    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Reservation>> GetExpiredPendingAsync(DateTimeOffset asOf, CancellationToken ct = default) =>
        await _context.Reservations
            .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt <= asOf)
            .ToListAsync(ct);

    public async Task AddAsync(Reservation reservation, CancellationToken ct = default) =>
        await _context.Reservations.AddAsync(reservation, ct);

    public void Update(Reservation reservation) => _context.Reservations.Update(reservation);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The only unique constraint in this DB is the active-seat index,
            // so a 23505 here means two reservations raced for the same seat.
            // Surface it as a domain-meaningful exception (API maps to 409).
            var clashing = _context.ChangeTracker.Entries<Reservation>()
                .FirstOrDefault(e => e.State == EntityState.Added)?.Entity;

            if (clashing is not null)
                throw new SeatAlreadyReservedException(
                    clashing.EventId, clashing.SeatSection, clashing.SeatRow, clashing.SeatNumber);

            throw;
        }
    }
}
