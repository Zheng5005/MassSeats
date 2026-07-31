using UserService.Application.DTOs;
using UserService.Application.Interfaces;
using UserService.Application.Mapping;
using UserService.Domain.Exceptions;
using UserService.Domain.Interfaces;

namespace UserService.Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public AuthService(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _repository.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var token = _tokenGenerator.GenerateToken(user);
        return new LoginResponse(token, user.ToResponse());
    }
}
