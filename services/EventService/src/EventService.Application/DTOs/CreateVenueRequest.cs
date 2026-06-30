namespace EventService.Application.DTOs;

public sealed record CreateVenueRequest(
    string Name,
    string Address,
    string City,
    string Country,
    int Capacity);
