namespace BookingService.Domain.Enums;

/// <summary>
/// Lifecycle of a seat reservation. Transitions are only allowed from
/// <see cref="Pending"/>; the terminal states never change again.
/// </summary>
public enum ReservationStatus
{
    /// <summary>Seat is held tentatively, waiting for payment. Can expire.</summary>
    Pending = 0,

    /// <summary>Payment succeeded; the seat belongs to the user.</summary>
    Confirmed = 1,

    /// <summary>Explicitly cancelled (by user or on payment failure).</summary>
    Cancelled = 2,

    /// <summary>The hold timed out before payment completed.</summary>
    Expired = 3
}
