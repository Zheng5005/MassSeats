using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Infrastructure.Persistence;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context) => _context = context;

    public Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Payments.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        _context.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId, ct);

    public Task<Payment?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct = default) =>
        _context.Payments.FirstOrDefaultAsync(p => p.StripePaymentIntentId == stripePaymentIntentId, ct);

    public async Task AddAsync(Payment payment, CancellationToken ct = default) =>
        await _context.Payments.AddAsync(payment, ct);

    public void Update(Payment payment) => _context.Payments.Update(payment);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The unique constraints are booking_id and stripe_payment_intent_id,
            // so a 23505 here means a concurrent create raced for the same
            // booking. Surface it as a typed exception so InitiateAsync can
            // return the existing payment instead of a raw 500.
            var clashing = _context.ChangeTracker.Entries<Payment>()
                .FirstOrDefault(e => e.State == EntityState.Added)?.Entity;

            if (clashing is not null)
                throw new DuplicatePaymentException(clashing.BookingId);

            throw;
        }
    }
}
