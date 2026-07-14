namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by PaymentService when a payment fails, so Booking can
/// cancel the associated reservation and release the seat.
/// </summary>
public sealed record PaymentFailed(
    Guid PaymentId,
    Guid BookingId,
    string Reason) : IntegrationEvent;
