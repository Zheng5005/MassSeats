namespace UserService.Application.DTOs;

public sealed record UpdateUserRequest(
    string FirstName,
    string? LastName,
    string? Phone,
    string? ProfileImage);
