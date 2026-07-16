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
}
