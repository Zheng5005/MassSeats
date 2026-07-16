namespace BookingService.Application;

/// <summary>
/// Application-owned policy for seat holds. The domain computes the
/// deadline but delegates the duration to the caller (this layer), so the
/// hold window is a business decision that can be tuned without touching
/// the entity.
/// </summary>
public sealed class ReservationOptions
{
    /// <summary>How long a Pending reservation holds the seat before it expires.</summary>
    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How often the background worker sweeps for expired holds. This is an
    /// operational cadence (how often we check), not a business rule like
    /// <see cref="HoldDuration"/> (how long the user has to pay).
    /// </summary>
    public TimeSpan ExpirationSweepInterval { get; set; } = TimeSpan.FromMinutes(1);
}
