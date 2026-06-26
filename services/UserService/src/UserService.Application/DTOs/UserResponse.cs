namespace UserService.Application.DTOs;

/// <summary>
/// Outbound representation of a user. Never exposes the password hash.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string? LastName,
    string Email,
    string? NationalId,
    string? ProfileImage,
    string? Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
