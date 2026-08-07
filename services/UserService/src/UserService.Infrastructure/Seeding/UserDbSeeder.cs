using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;
using UserService.Domain.Interfaces;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Seeding;

/// <summary>
/// Seeds a single demo user on startup. Guarded three ways so it never
/// runs outside Development or against a non-empty Users table.
/// </summary>
public sealed class UserDbSeeder
{
    private const string EnabledKey = "SeedData:Enabled";

    private readonly UserDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserDbSeeder> _logger;

    public UserDbSeeder(
        UserDbContext context,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<UserDbSeeder> logger)
    {
        _context = context;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!IsEnabled())
            return;

        if (await _context.Users.AnyAsync(ct))
            return;

        var hash = _passwordHasher.Hash("Demo123!");
        var user = User.Create(
            firstName: "Demo",
            lastName: "User",
            email: "demo@massseats.dev",
            passwordHash: hash);

        await _userRepository.AddAsync(user, ct);
        await _userRepository.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded demo user '{Email}' with password '{Password}'.",
            user.Email,
            "Demo123!");
    }

    private bool IsEnabled() =>
        string.Equals(
            _configuration[EnabledKey],
            "true",
            StringComparison.OrdinalIgnoreCase);
}
