using PaymentService.Application.DTOs;
using PaymentService.Domain.Entities;

namespace PaymentService.Application.Mapping;

/// <summary>
/// Manual mapping entity -> DTO. Kept explicit so the projection is
/// obvious and testable.
/// </summary>
internal static class PaymentMappings
{
    public static PaymentResponse ToResponse(this Payment payment) => new(
        payment.Id,
        payment.BookingId,
        payment.StripePaymentIntentId,
        payment.Amount,
        payment.Currency,
        payment.PaymentMethod,
        payment.Status.ToString(),
        payment.CreatedAt,
        payment.UpdatedAt);
}
