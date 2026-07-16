namespace BookingService.Application.DTOs;

/// <summary>
/// Inbound request to hold a seat tentatively (Pending) for an event.
/// </summary>
/// <remarks>
/// <c>UserId</c> and <c>Price</c> are carried in the request for now.
/// Once messaging is in place, <c>UserId</c> should come from the
/// authenticated caller and <c>Price</c> from the event data replicated
/// via <c>EventCreated</c> — not trusted from the client.
/// </remarks>
public sealed record CreateReservationRequest(
    Guid UserId,
    Guid EventId,
    string SeatSection,
    string SeatRow,
    int SeatNumber,
    decimal Price);
