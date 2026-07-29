using BookingService.Domain.Interfaces;
using BookingService.Infrastructure.BackgroundJobs;
using BookingService.Infrastructure.Messaging;
using BookingService.Infrastructure.Persistence;
using BuildingBlocks.Messaging.RabbitMQ;
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

        services.AddSingleton<OutboxSaveChangesInterceptor>();

        services.AddDbContext<BookingDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>()));

        services.AddScoped<IReservationRepository, ReservationRepository>();

        services.AddRabbitMqMessaging(configuration);
        services.AddHostedService<ReservationExpirationWorker>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }
}
