using PaymentService.Application.DTOs;

namespace PaymentService.Application.Interfaces;

/// <summary>
/// Application service that orchestrates payment use cases and coordinates
/// with Stripe via <see cref="IPaymentGateway"/>.
/// </summary>
public interface IPaymentService
{
    /// <summary>Initiates a payment (creates Stripe PaymentIntent).</summary>
    Task<PaymentResponse> InitiateAsync(InitiatePaymentRequest request, CancellationToken ct = default);

    Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Finds a payment by booking ID.</summary>
    Task<PaymentResponse?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);

    /// <summary>Processes a Stripe webhook event (succeed/fail the payment).</summary>
    Task<PaymentResponse> HandleWebhookAsync(StripeWebhookRequest request, CancellationToken ct = default);
}
