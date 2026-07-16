using BookingService.Application.DTOs;

namespace BookingService.Application.Interfaces;

/// <summary>
/// Application service that orchestrates the reservation use cases and
/// coordinates the choreography saga (create hold, confirm on payment,
/// cancel on failure or user request).
/// </summary>
public interface IReservationService
{
    /// <summary>
    /// Creates a Pending reservation. Seat uniqueness is enforced by a
    /// database constraint; a clash surfaces as
    /// <see cref="Domain.Exceptions.SeatAlreadyReservedException"/>.
    /// </summary>
    Task<ReservationResponse> CreateAsync(CreateReservationRequest request, CancellationToken ct = default);

    Task<ReservationResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Confirms a Pending reservation after a successful payment.</summary>
    Task<ReservationResponse> ConfirmAsync(Guid id, ConfirmReservationRequest request, CancellationToken ct = default);

    /// <summary>Cancels a Pending reservation (user request or payment failure).</summary>
    Task<ReservationResponse> CancelAsync(Guid id, CancellationToken ct = default);
}
