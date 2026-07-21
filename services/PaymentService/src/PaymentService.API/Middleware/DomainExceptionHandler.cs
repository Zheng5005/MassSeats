using BuildingBlocks.Domain;
using PaymentService.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PaymentService.API.Middleware;

/// <summary>
/// Translates domain exceptions into RFC 7807 ProblemDetails responses,
/// keeping HTTP concerns out of the Application/Domain layers.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            PaymentNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            InvalidPaymentStateException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status400BadRequest, "Domain rule violation"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid argument"),
            _ => (0, string.Empty)
        };

        // Not a domain exception: let the default handler deal with it.
        if (status == 0) return false;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message
        }, cancellationToken);

        return true;
    }
}
