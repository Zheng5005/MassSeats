namespace PaymentService.Domain.Enums;

/// <summary>
/// Lifecycle of a payment. Transitions: Pending -> Succeeded / Failed.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Payment has been initiated but not yet completed.</summary>
    Pending = 0,

    /// <summary>Payment succeeded (Stripe confirmed).</summary>
    Succeeded = 1,

    /// <summary>Payment failed or was declined.</summary>
    Failed = 2
}
