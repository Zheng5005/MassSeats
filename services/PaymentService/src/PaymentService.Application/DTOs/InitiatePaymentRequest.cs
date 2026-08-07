namespace PaymentService.Application.DTOs;

/// <summary>
/// Inbound request to initiate a payment for a completed booking.
/// Triggered by the <c>SeatReserved</c> integration event consumer.
/// </summary>
public sealed record InitiatePaymentRequest(
    Guid UserId,
    Guid BookingId,
    decimal Amount,
    string Currency);
