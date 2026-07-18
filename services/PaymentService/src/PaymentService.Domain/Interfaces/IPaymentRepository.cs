using PaymentService.Domain.Entities;

namespace PaymentService.Domain.Interfaces;

/// <summary>
/// Persistence contract for the Payment aggregate. Defined in the domain
/// (the inner layer) and implemented in Infrastructure.
/// </summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Finds a payment by its associated booking ID.</summary>
    Task<Payment?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);

    /// <summary>
    /// Finds a payment by its Stripe PaymentIntent ID. Used when handling
    /// Stripe webhooks, which identify the payment by that ID.
    /// </summary>
    Task<Payment?> GetByStripePaymentIntentIdAsync(string stripePaymentIntentId, CancellationToken ct = default);

    Task AddAsync(Payment payment, CancellationToken ct = default);
    void Update(Payment payment);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
