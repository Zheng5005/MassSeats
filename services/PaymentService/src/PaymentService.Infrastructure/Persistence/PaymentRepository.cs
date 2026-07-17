using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
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

    public async Task AddAsync(Payment payment, CancellationToken ct = default) =>
        await _context.Payments.AddAsync(payment, ct);

    public void Update(Payment payment) => _context.Payments.Update(payment);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct);
}
