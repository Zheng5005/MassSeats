using Microsoft.AspNetCore.Identity;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Security;

/// <summary>
/// Adapter over ASP.NET Core Identity's PBKDF2 hasher. Implements the
/// Application port so the rest of the app never touches crypto directly.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) =>
        _inner.HashPassword(user: null!, password);

    public bool Verify(string password, string passwordHash)
    {
        var result = _inner.VerifyHashedPassword(user: null!, passwordHash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
