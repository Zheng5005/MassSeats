using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Interfaces;
using UserService.Application.Services;

namespace UserService.Application;

/// <summary>
/// Composition helper for the Application layer. The API calls this so
/// it doesn't need to know the concrete service types.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserAppService>();
        return services;
    }
}
