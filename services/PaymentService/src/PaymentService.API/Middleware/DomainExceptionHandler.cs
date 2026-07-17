using System.Net;
using System.Text.Json;
using BuildingBlocks.Domain;

namespace PaymentService.API.Middleware;

/// <summary>
/// Catches <see cref="DomainException"/> and returns structured ProblemDetails
/// responses. Keeps the endpoint code clean of try/catch for domain errors.
/// </summary>
public sealed class DomainExceptionHandler
{
    private readonly RequestDelegate _next;

    public DomainExceptionHandler(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            context.Response.ContentType = "application/problem+json";

            // Map exception type to HTTP status
            var statusCode = ex switch
            {
                // 404
                Domain.Exceptions.PaymentNotFoundException => HttpStatusCode.NotFound,
                // 409
                Domain.Exceptions.InvalidPaymentStateException => HttpStatusCode.Conflict,
                // 400 (default)
                _ => HttpStatusCode.BadRequest,
            };

            context.Response.StatusCode = (int)statusCode;

            var problem = new
            {
                type = $"https://httpstatuses.io/{(int)statusCode}",
                title = statusCode switch
                {
                    HttpStatusCode.BadRequest => "Bad Request",
                    HttpStatusCode.NotFound => "Not Found",
                    HttpStatusCode.Conflict => "Conflict",
                    _ => "Error",
                },
                status = (int)statusCode,
                detail = ex.Message,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }
}
