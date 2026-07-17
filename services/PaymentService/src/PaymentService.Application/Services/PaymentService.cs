using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Mapping;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Application.Services;

/// <summary>
/// Application service that orchestrates the payment use cases.
/// Coordinates the domain aggregate, the repository, and the Stripe gateway.
/// </summary>
public sealed class PaymentAppService : IPaymentService
{
    private readonly IPaymentRepository _repository;
    private readonly IPaymentGateway _gateway;

    public PaymentAppService(IPaymentRepository repository, IPaymentGateway gateway)
    {
        _repository = repository;
        _gateway = gateway;
    }

    public async Task<PaymentResponse> InitiateAsync(InitiatePaymentRequest request, CancellationToken ct = default)
    {
        // 1. Create PaymentIntent in Stripe
        var stripePi = await _gateway.CreatePaymentIntentAsync(request.Amount, request.Currency, ct);

        // 2. Create domain aggregate
        var payment = Domain.Entities.Payment.Create(
            request.BookingId,
            stripePi,
            request.Amount,
            request.Currency);

        // 3. Persist
        await _repository.AddAsync(payment, ct);
        await _repository.SaveChangesAsync(ct);

        return payment.ToResponse();
    }

    public async Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var payment = await _repository.GetByIdAsync(id, ct);
        return payment?.ToResponse();
    }

    public async Task<PaymentResponse?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default)
    {
        var payment = await _repository.GetByBookingIdAsync(bookingId, ct);
        return payment?.ToResponse();
    }

    public async Task<PaymentResponse> HandleWebhookAsync(StripeWebhookRequest request, CancellationToken ct = default)
    {
        // Find payment by Stripe PaymentIntent ID, but we need a lookup...
        // For now, we look up by the raw body/event context.
        // The repository needs a GetByStripePaymentIntentId method, but
        // that's an Infrastructure concern. We'll use the domain events flow.
        // Placeholder — will be implemented when wiring the webhook.
        throw new NotImplementedException("Webhook handling will be implemented with the integration layer.");
    }
}
