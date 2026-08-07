using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Messaging;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments").WithTags("Payments");

        group.MapGet("/{id:guid}", async (HttpContext httpContext, Guid id, IPaymentService service, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null)
                return Results.Unauthorized();

            var payment = await service.GetByIdForUserAsync(id, userId.Value, ct);
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        });

        // In-browser checkout: returns the Stripe client secret so the browser
        // can confirm the payment with Stripe Elements. Only exposed while the
        // payment is Pending — a resolved payment returns the same 404 as a
        // missing one (nothing left to confirm). The secret never appears in
        // the general read model (GET /payments/{id}).
        group.MapGet("/{bookingId:guid}/client-secret", async (HttpContext httpContext, Guid bookingId, IPaymentService service, CancellationToken ct) =>
        {
            var userId = GetUserId(httpContext);
            if (userId is null)
                return Results.Unauthorized();

            var result = await service.GetClientSecretForUserAsync(bookingId, userId.Value, ct);
            if (result is null)
                return Results.NotFound();

            return Results.Ok(new
            {
                clientSecret = result.ClientSecret,
                paymentIntentId = result.PaymentIntentId
            });
        });

        // Called by Stripe (not RabbitMQ). The signature is verified against
        // the RAW request body, so we read the body ourselves instead of
        // binding a DTO — deserializing first would change the bytes and
        // break signature verification.
        group.MapPost("/webhook", async (HttpRequest request, IPaymentGateway gateway, StripeWebhookProcessor processor, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["Stripe-Signature"].ToString();

            // Authenticate the request at the boundary. Invalid signature =>
            // untrusted => 400 (and the Application never sees it).
            var webhookEvent = await gateway.VerifyWebhookAsync(rawBody, signature, ct);
            if (webhookEvent is null)
                return Results.BadRequest("Invalid webhook signature.");

            var payment = await processor.ProcessAsync(webhookEvent, ct);
            return payment is null ? Results.Ok() : Results.Ok(payment);
        });

        return app;
    }

    // The gateway injects the authenticated user id as a header. Missing or
    // unparseable header => unauthenticated. Scope is enforced in the service,
    // so a wrong user id yields the same 404 as a missing payment.
    private static Guid? GetUserId(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers["X-User-Id"].FirstOrDefault();
        return Guid.TryParse(header, out var userId) ? userId : null;
    }
}
