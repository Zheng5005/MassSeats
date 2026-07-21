using PaymentService.Application.Interfaces;

namespace PaymentService.API.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments").WithTags("Payments");

        group.MapGet("/{id:guid}", async (Guid id, IPaymentService service, CancellationToken ct) =>
        {
            var payment = await service.GetByIdAsync(id, ct);
            return payment is null ? Results.NotFound() : Results.Ok(payment);
        });

        // Called by Stripe (not RabbitMQ). The signature is verified against
        // the RAW request body, so we read the body ourselves instead of
        // binding a DTO — deserializing first would change the bytes and
        // break signature verification.
        group.MapPost("/webhook", async (HttpRequest request, IPaymentGateway gateway, IPaymentService service, CancellationToken ct) =>
        {
            using var reader = new StreamReader(request.Body);
            var rawBody = await reader.ReadToEndAsync(ct);
            var signature = request.Headers["Stripe-Signature"].ToString();

            // Authenticate the request at the boundary. Invalid signature =>
            // untrusted => 400 (and the Application never sees it).
            var webhookEvent = await gateway.VerifyWebhookAsync(rawBody, signature, ct);
            if (webhookEvent is null)
                return Results.BadRequest("Invalid webhook signature.");

            var payment = await service.HandleWebhookAsync(webhookEvent, ct);
            return Results.Ok(payment);
        });

        return app;
    }
}
