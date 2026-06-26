namespace UserService.Application.DTOs;

public sealed record CreateUserRequest(
    string FirstName,
    string? LastName,
    string Email,
    string Password,
    string? NationalId,
    string? Phone);
