using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Configuration;
using Stripe;

namespace PaymentService.Infrastructure.Gateways;

/// <summary>
/// Concrete adapter for the <see cref="IPaymentGateway"/> port, backed by
/// the Stripe SDK. This is the ONLY place that references Stripe directly —
/// the domain and application stay ignorant of the provider.
/// </summary>
public sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeClient _client;
    private readonly string _webhookSecret;

    public StripePaymentGateway(StripeOptions options)
    {
        _client = new StripeClient(options.SecretKey);
        _webhookSecret = options.WebhookSecret;
    }

    public async Task<string> CreatePaymentIntentAsync(decimal amount, string currency, CancellationToken ct = default)
    {
        var service = new PaymentIntentService(_client);

        var options = new PaymentIntentCreateOptions
        {
            // Stripe expects the amount in the smallest currency unit (e.g.
            // cents). This assumes a 2-decimal currency (USD, EUR...). Zero-
            // decimal currencies (JPY) would need different handling.
            Amount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
            Currency = currency.ToLowerInvariant(),
        };

        var intent = await service.CreateAsync(options, cancellationToken: ct);
        return intent.Id;
    }

    public Task<StripeWebhookResult?> VerifyWebhookAsync(string rawBody, string signatureHeader, CancellationToken ct = default)
    {
        try
        {
            // Throws StripeException if the signature does not match the
            // raw body with our webhook secret — i.e. an untrusted request.
            var stripeEvent = EventUtility.ConstructEvent(rawBody, signatureHeader, _webhookSecret);

            if (stripeEvent.Data.Object is not PaymentIntent intent)
                return Task.FromResult<StripeWebhookResult?>(null);

            var result = new StripeWebhookResult(
                StripeEventId: stripeEvent.Id,
                StripePaymentIntentId: intent.Id,
                EventType: stripeEvent.Type,
                PaymentMethod: intent.PaymentMethodTypes?.FirstOrDefault(),
                FailureReason: intent.LastPaymentError?.Message);

            return Task.FromResult<StripeWebhookResult?>(result);
        }
        catch (StripeException)
        {
            // Invalid signature or malformed payload → treat as unverified.
            return Task.FromResult<StripeWebhookResult?>(null);
        }
    }
}
