using Microsoft.EntityFrameworkCore;
using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// First line of Stripe webhook deduplication at the Payment edge. Each
/// verified event id is claimed in the processed_stripe_events table inside
/// the same transaction as the payment state transition, so replayed or
/// duplicate deliveries never re-enter the Application service. The state
/// guard in <see cref="IPaymentService.HandleWebhookAsync"/> remains as a
/// second line of defence for payments that already transitioned.
/// </summary>
public sealed class StripeWebhookProcessor
{
    private readonly PaymentDbContext _dbContext;
    private readonly IPaymentService _paymentService;

    public StripeWebhookProcessor(PaymentDbContext dbContext, IPaymentService paymentService)
    {
        _dbContext = dbContext;
        _paymentService = paymentService;
    }

    /// <summary>
    /// Claims the webhook event id and processes the payment transition in a
    /// single transaction. Returns null when the event id was already
    /// processed (duplicate delivery).
    /// </summary>
    /// <remarks>
    /// Exceptions are intentionally NOT caught: the transaction is disposed
    /// and rolled back, and the error propagates so the endpoint returns an
    /// error and Stripe redelivers later. A transient failure (e.g., the
    /// payment does not exist yet) must NOT burn the claim.
    /// </remarks>
    public async Task<PaymentResponse?> ProcessAsync(
        StripeWebhookResult webhookEvent,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO processed_stripe_events (stripe_event_id, processed_on) VALUES ({webhookEvent.StripeEventId}, {DateTimeOffset.UtcNow}) ON CONFLICT (stripe_event_id) DO NOTHING",
            cancellationToken);

        if (claimed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var result = await _paymentService.HandleWebhookAsync(webhookEvent, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
