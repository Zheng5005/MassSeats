using BookingService.Domain.Interfaces;
using BookingService.Infrastructure.BackgroundJobs;
using BookingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BookingService.Infrastructure;

/// <summary>
/// Wires up Infrastructure concretions (EF Core DbContext, repository,
/// background worker) behind the abstractions defined in the inner layers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BookingDb")
            ?? throw new InvalidOperationException("Connection string 'BookingDb' is not configured.");

        services.AddDbContext<BookingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IReservationRepository, ReservationRepository>();

        services.AddHostedService<ReservationExpirationWorker>();

        return services;
    }
}
