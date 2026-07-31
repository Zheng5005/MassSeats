using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
        };
    });

builder.Services.AddAuthorization();

// YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    }
});

app.UseAuthentication();

// Auth middleware: validate JWT and forward user claims as headers
// Excluded paths (login, webhook) don't need authentication
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

    // Skip auth for public endpoints
    if (path.StartsWith("/users/login") || path.StartsWith("/payments/webhook"))
    {
        await next(context);
        return;
    }

    // Reject unauthenticated requests
    if (context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(
            """{"title":"Unauthorized","status":401,"detail":"Valid JWT token is required."}""");
        return;
    }

    // Forward user identity to upstream services via headers
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = context.User.FindFirst(ClaimTypes.Email)?.Value;
    var name = context.User.FindFirst(ClaimTypes.GivenName)?.Value;

    if (userId is not null)
        context.Request.Headers["X-User-Id"] = userId;
    if (email is not null)
        context.Request.Headers["X-User-Email"] = email;
    if (name is not null)
        context.Request.Headers["X-User-Name"] = name;

    await next(context);
});

app.UseAuthorization();
app.MapReverseProxy();

app.Run();
