using EventService.Application.DTOs;
using EventService.Application.Interfaces;

namespace EventService.API.Endpoints;

public static class VenueEndpoints
{
    public static IEndpointRouteBuilder MapVenueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/venues").WithTags("Venues");

        group.MapPost("/", async (CreateVenueRequest request, IVenueService service, CancellationToken ct) =>
        {
            var venue = await service.CreateAsync(request, ct);
            return Results.Created($"/venues/{venue.Id}", venue);
        });

        group.MapGet("/", async (IVenueService service, CancellationToken ct) =>
        {
            var venues = await service.GetAllAsync(ct);
            return Results.Ok(venues);
        });

        group.MapGet("/{id:guid}", async (Guid id, IVenueService service, CancellationToken ct) =>
        {
            var venue = await service.GetByIdAsync(id, ct);
            return venue is null ? Results.NotFound() : Results.Ok(venue);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateVenueRequest request, IVenueService service, CancellationToken ct) =>
        {
            var venue = await service.UpdateAsync(id, request, ct);
            return Results.Ok(venue);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IVenueService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
