namespace PaymentService.Application.DTOs;

/// <summary>
/// Represents the parsed payload of an incoming Stripe webhook event
/// after signature verification and deserialization.
/// </summary>
public sealed record StripeWebhookRequest(
    string StripeEventId,
    string StripePaymentIntentId,
    string EventType,
    string? PaymentMethod);
