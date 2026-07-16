namespace BookingService.Application.DTOs;

/// <summary>
/// Inbound request to confirm a Pending reservation after a successful
/// payment. Triggered by the <c>PaymentSucceeded</c> consumer (or a
/// direct call while messaging is not yet wired).
/// </summary>
public sealed record ConfirmReservationRequest(Guid PaymentId);
