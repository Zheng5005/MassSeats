using BuildingBlocks.Domain;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Events;
using PaymentService.Domain.Exceptions;

namespace PaymentService.Domain.Entities;

/// <summary>
/// Aggregate root that represents a payment for a booking.
/// Owns its lifecycle (Pending -> Succeeded / Failed) and raises domain
/// events so the outbox can turn them into integration events for the saga.
/// No public setters — state only changes through behavior methods.
/// </summary>
public sealed class Payment : AggregateRoot
{
    public Guid UserId { get; private set; }
    public Guid BookingId { get; private set; }
    public string StripePaymentIntentId { get; private set; }
    public string ClientSecret { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public string? PaymentMethod { get; private set; }
    public string? FailureReason { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Required by EF Core to materialize entities; not for application code.
    private Payment()
    {
        StripePaymentIntentId = null!;
        ClientSecret = null!;
        Currency = null!;
    }

    private Payment(
        Guid id,
        Guid userId,
        Guid bookingId,
        string stripePaymentIntentId,
        string clientSecret,
        decimal amount,
        string currency) : base(id)
    {
        UserId = userId;
        BookingId = bookingId;
        StripePaymentIntentId = stripePaymentIntentId;
        ClientSecret = clientSecret;
        Amount = amount;
        Currency = currency;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a Pending payment linked to a booking and a Stripe PaymentIntent.
    /// </summary>
    public static Payment Create(
        Guid userId,
        Guid bookingId,
        string stripePaymentIntentId,
        string clientSecret,
        decimal amount,
        string currency)
    {
        if (userId == Guid.Empty)
            throw new DomainValidationException("UserId is required.");
        if (bookingId == Guid.Empty)
            throw new DomainValidationException("BookingId is required.");
        if (string.IsNullOrWhiteSpace(stripePaymentIntentId))
            throw new DomainValidationException("Stripe PaymentIntent ID is required.");
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new DomainValidationException("Client secret is required.");
        if (amount <= 0)
            throw new DomainValidationException("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainValidationException("Currency is required.");

        var payment = new Payment(
            id: Guid.NewGuid(),
            userId: userId,
            bookingId: bookingId,
            stripePaymentIntentId: stripePaymentIntentId.Trim(),
            clientSecret: clientSecret.Trim(),
            amount: amount,
            currency: currency.Trim().ToUpperInvariant());

        payment.RaiseDomainEvent(new PaymentInitiatedDomainEvent(
            payment.Id,
            payment.BookingId,
            payment.Amount,
            payment.Currency));

        return payment;
    }

    /// <summary>Marks the payment as succeeded (confirmed by Stripe webhook).</summary>
    public void Succeed(string? paymentMethod = null)
    {
        EnsurePending(nameof(Succeed));

        PaymentMethod = paymentMethod;
        Status = PaymentStatus.Succeeded;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PaymentSucceededDomainEvent(Id, BookingId));
    }

    /// <summary>Marks the payment as failed (Stripe declined or error).</summary>
    public void Fail(string reason)
    {
        EnsurePending(nameof(Fail));

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainValidationException("A failure reason is required.");

        FailureReason = reason.Trim();
        Status = PaymentStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PaymentFailedDomainEvent(Id, BookingId, reason.Trim()));
    }

    private void EnsurePending(string action)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidPaymentStateException(Id, Status, action.ToLowerInvariant());
    }
}
