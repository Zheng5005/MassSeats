using Microsoft.Extensions.DependencyInjection;
using EventService.Application.Interfaces;
using EventService.Application.Services;

namespace EventService.Application;

/// <summary>
/// Composition helper for the Application layer. The API calls this so
/// it doesn't need to know the concrete service types.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventAppService>();
        services.AddScoped<IVenueService, VenueAppService>();
        return services;
    }
}
