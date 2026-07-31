namespace UserService.Application.DTOs;

public sealed record LoginResponse(string Token, UserResponse User);
