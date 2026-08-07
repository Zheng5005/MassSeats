using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Mapping;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
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
        // Idempotency: one payment per booking. If SeatReserved is delivered
        // twice, return the existing payment instead of creating a second
        // Stripe PaymentIntent for the same booking.
        var existing = await _repository.GetByBookingIdAsync(request.BookingId, ct);
        if (existing is not null)
            return existing.ToResponse();

        // 1. Create the PaymentIntent in Stripe (external side effect first).
        var result = await _gateway.CreatePaymentIntentAsync(
            request.BookingId, request.Amount, request.Currency, ct);

        // 2. Create and persist the domain aggregate (Pending).
        var payment = Payment.Create(
            request.BookingId,
            result.Id,
            result.ClientSecret,
            request.Amount,
            request.Currency);

        try
        {
            await _repository.AddAsync(payment, ct);
            await _repository.SaveChangesAsync(ct);
        }
        catch (DuplicatePaymentException)
        {
            // Lost a concurrent create race: another delivery already
            // persisted the payment for this booking. Return the committed
            // payment instead of surfacing the raw 23505 as a 500.
            var committed = await _repository.GetByBookingIdAsync(request.BookingId, ct);
            if (committed is not null)
                return committed.ToResponse();

            throw;
        }

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

    public async Task<string?> GetClientSecretAsync(Guid bookingId, CancellationToken ct = default)
    {
        var payment = await _repository.GetByBookingIdAsync(bookingId, ct);
        return payment is { Status: PaymentStatus.Pending } ? payment.ClientSecret : null;
    }

    public async Task<PaymentResponse> HandleWebhookAsync(StripeWebhookResult webhookEvent, CancellationToken ct = default)
    {
        var payment = await _repository.GetByStripePaymentIntentIdAsync(webhookEvent.StripePaymentIntentId, ct)
                      ?? throw new PaymentNotFoundException(webhookEvent.StripePaymentIntentId);

        // Idempotency: a Stripe webhook may be delivered more than once. Once
        // the payment left Pending we already processed it — return the current
        // state without re-applying the transition (no double confirm/charge).
        if (payment.Status != PaymentStatus.Pending)
            return payment.ToResponse();

        switch (webhookEvent.EventType)
        {
            case "payment_intent.succeeded":
                payment.Succeed(webhookEvent.PaymentMethod);
                break;

            case "payment_intent.payment_failed":
                payment.Fail(webhookEvent.FailureReason ?? "Payment failed at Stripe.");
                break;

            default:
                // An event type we don't act on: acknowledge without changing state.
                return payment.ToResponse();
        }

        _repository.Update(payment);
        await _repository.SaveChangesAsync(ct);

        return payment.ToResponse();
    }
}
