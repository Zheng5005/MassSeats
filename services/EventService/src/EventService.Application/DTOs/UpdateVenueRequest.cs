namespace EventService.Application.DTOs;

public sealed record UpdateVenueRequest(
    string Name,
    string Address,
    string City,
    string Country,
    int Capacity);
