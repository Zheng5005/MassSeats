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

    /// <summary>
    /// Processes an already-verified Stripe webhook event (succeed/fail the
    /// payment). Signature verification is done at the API boundary via
    /// <see cref="IPaymentGateway.VerifyWebhookAsync"/>; this method receives
    /// the trusted, parsed event. Idempotent: replayed events are no-ops.
    /// </summary>
    Task<PaymentResponse> HandleWebhookAsync(StripeWebhookResult webhookEvent, CancellationToken ct = default);
}
