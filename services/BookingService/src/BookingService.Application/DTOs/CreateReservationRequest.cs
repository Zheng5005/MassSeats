namespace BookingService.Application.DTOs;

/// <summary>
/// Inbound request to hold a seat tentatively (Pending) for an event.
/// </summary>
/// <remarks>
/// <c>UserId</c> is extracted from the <c>X-User-Id</c> header injected
/// by the API Gateway — not trusted from the client. <c>Price</c> should
/// eventually come from the event data replicated via <c>EventCreated</c>
/// once messaging is wired for it.
/// </remarks>
public sealed record CreateReservationRequest(
    Guid EventId,
    string SeatSection,
    string SeatRow,
    int SeatNumber,
    decimal Price);
