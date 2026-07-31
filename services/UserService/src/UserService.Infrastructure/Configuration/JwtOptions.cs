namespace UserService.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 signing key (min 32 chars).</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Token issuer — identifies who issued the JWT.</summary>
    public string Issuer { get; set; } = "MassSeats";

    /// <summary>Intended audience — the API the token is for.</summary>
    public string Audience { get; set; } = "massseats-api";

    /// <summary>Token lifetime in minutes (default 60).</summary>
    public int ExpiryMinutes { get; set; } = 60;
}
