using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;

/// <summary>Application port for JWT token generation.</summary>
public interface ITokenGenerator
{
    string GenerateToken(User user);
}
