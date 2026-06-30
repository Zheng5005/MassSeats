namespace EventService.Application.DTOs;

/// <summary>
/// Outbound representation of an event category.
/// </summary>
public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
