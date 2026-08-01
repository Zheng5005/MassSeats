using EventService.Domain.Interfaces;
using EventService.Infrastructure.Messaging;
using EventService.Infrastructure.Persistence;
using BuildingBlocks.Messaging.Contracts;
using BuildingBlocks.Messaging.RabbitMQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventService.Infrastructure;

/// <summary>
/// Wires up Infrastructure concretions (EF Core DbContext, repositories)
/// behind the abstractions defined in the inner layers.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("EventDb")
            ?? throw new InvalidOperationException("Connection string 'EventDb' is not configured.");

        services.AddSingleton<OutboxSaveChangesInterceptor>();

        services.AddDbContext<EventDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(connectionString)
                .AddInterceptors(serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>()));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();

        services.AddRabbitMqMessaging(configuration);
        services.AddEventConsumer<SeatReserved, SeatReservedConsumer>();
        services.AddEventConsumer<ReservationConfirmed, ReservationConfirmedConsumer>();
        services.AddEventConsumer<ReservationCancelled, ReservationCancelledConsumer>();
        services.AddEventConsumer<ReservationExpired, ReservationExpiredConsumer>();
        services.AddHostedService<OutboxPublisherWorker>();

        return services;
    }
}
