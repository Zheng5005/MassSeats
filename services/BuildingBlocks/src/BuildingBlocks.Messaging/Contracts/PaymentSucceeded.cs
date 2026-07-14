namespace BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Published by PaymentService when a payment succeeds, so Booking can
/// confirm the associated reservation.
/// </summary>
public sealed record PaymentSucceeded(
    Guid PaymentId,
    Guid BookingId) : IntegrationEvent;
