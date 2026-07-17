namespace PaymentService.Application.DTOs;

/// <summary>
/// Outbound representation of a payment.
/// </summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid BookingId,
    string StripePaymentIntentId,
    decimal Amount,
    string Currency,
    string? PaymentMethod,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
