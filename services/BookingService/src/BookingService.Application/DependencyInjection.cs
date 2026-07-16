using BookingService.Application.Interfaces;
using BookingService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BookingService.Application;

/// <summary>
/// Composition helper for the Application layer. The API calls this so
/// it doesn't need to know the concrete service types.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ReservationOptions>();
        services.AddScoped<IReservationService, ReservationAppService>();
        return services;
    }
}
