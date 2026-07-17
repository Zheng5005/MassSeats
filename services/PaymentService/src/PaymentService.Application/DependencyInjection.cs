using PaymentService.Application.Interfaces;
using PaymentService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentService.Application;

/// <summary>
/// Composition helper for the Application layer. The API calls this so
/// it doesn't need to know the concrete service types.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPaymentService, PaymentAppService>();
        return services;
    }
}
