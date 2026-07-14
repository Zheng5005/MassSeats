using EventService.Application.DTOs;
using EventService.Application.Interfaces;

namespace EventService.API.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events").WithTags("Events");

        group.MapPost("/", async (CreateEventRequest request, IEventService service, CancellationToken ct) =>
        {
            var @event = await service.CreateAsync(request, ct);
            return Results.Created($"/events/{@event.Id}", @event);
        });

        group.MapGet("/", async (IEventService service, CancellationToken ct) =>
        {
            var events = await service.GetAllAsync(ct);
            return Results.Ok(events);
        });

        group.MapGet("/{id:guid}", async (Guid id, IEventService service, CancellationToken ct) =>
        {
            var @event = await service.GetByIdAsync(id, ct);
            return @event is null ? Results.NotFound() : Results.Ok(@event);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest request, IEventService service, CancellationToken ct) =>
        {
            var @event = await service.UpdateAsync(id, request, ct);
            return Results.Ok(@event);
        });

        group.MapPut("/{id:guid}/pricing", async (Guid id, UpdateEventPricingRequest request, IEventService service, CancellationToken ct) =>
        {
            var @event = await service.UpdatePricingAsync(id, request, ct);
            return Results.Ok(@event);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IEventService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        // Categories are read-only reference data served by the Event service.
        app.MapGet("/categories", async (IEventService service, CancellationToken ct) =>
        {
            var categories = await service.GetCategoriesAsync(ct);
            return Results.Ok(categories);
        }).WithTags("Categories");

        return app;
    }
}
