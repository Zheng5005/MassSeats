namespace PaymentService.Infrastructure.Configuration;

/// <summary>
/// Stripe credentials, bound from the "Stripe" configuration section.
/// Kept out of source control in real environments (user-secrets / env vars).
/// </summary>
public sealed class StripeOptions
{
    /// <summary>Secret API key used to call Stripe (create PaymentIntents).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Signing secret used to verify incoming webhook signatures.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
