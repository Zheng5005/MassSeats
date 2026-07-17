namespace PaymentService.Application.Interfaces;

/// <summary>
/// Port abstraction for the Stripe SDK. Defined in Application so the
/// domain and application never reference Stripe directly.
/// Infrastructure provides the concrete implementation.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Creates a PaymentIntent in Stripe and returns its ID.</summary>
    Task<string> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies the Stripe webhook signature and returns the parsed event.
    /// Returns null when the signature is invalid.
    /// </summary>
    Task<StripeWebhookResult?> VerifyWebhookAsync(
        string rawBody,
        string signatureHeader,
        CancellationToken ct = default);
}

/// <summary>Parsed result of a verified Stripe webhook event.</summary>
public sealed record StripeWebhookResult(
    string StripeEventId,
    string StripePaymentIntentId,
    string EventType,
    string? PaymentMethod);
