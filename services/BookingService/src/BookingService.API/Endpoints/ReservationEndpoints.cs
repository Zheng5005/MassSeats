using BookingService.Application.DTOs;
using BookingService.Application.Interfaces;

namespace BookingService.API.Endpoints;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reservations").WithTags("Reservations");

        group.MapPost("/", async (CreateReservationRequest request, IReservationService service, CancellationToken ct) =>
        {
            var reservation = await service.CreateAsync(request, ct);
            return Results.Created($"/reservations/{reservation.Id}", reservation);
        });

        group.MapGet("/{id:guid}", async (Guid id, IReservationService service, CancellationToken ct) =>
        {
            var reservation = await service.GetByIdAsync(id, ct);
            return reservation is null ? Results.NotFound() : Results.Ok(reservation);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IReservationService service, CancellationToken ct) =>
        {
            await service.CancelAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
