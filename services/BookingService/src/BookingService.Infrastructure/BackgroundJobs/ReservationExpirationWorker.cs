using BookingService.Application;
using BookingService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingService.Infrastructure.BackgroundJobs;

/// <summary>
/// Background worker that periodically releases seats held by Pending
/// reservations whose deadline has passed. It only schedules the sweep;
/// the actual expiration logic lives in the Application layer
/// (<see cref="IReservationService.ExpireDueReservationsAsync"/>).
/// </summary>
public sealed class ReservationExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationOptions _options;
    private readonly ILogger<ReservationExpirationWorker> _logger;

    public ReservationExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ReservationOptions options,
        ILogger<ReservationExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reservation expiration worker started; sweeping every {Interval}.",
            _options.ExpirationSweepInterval);

        // PeriodicTimer gives a clean, allocation-free loop that never
        // overlaps ticks: the next wait only starts after the body finishes.
        using var timer = new PeriodicTimer(_options.ExpirationSweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                // The application service is Scoped (it owns a DbContext),
                // but a BackgroundService is a Singleton. We must open a
                // fresh scope per tick to resolve it safely.
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IReservationService>();

                var expired = await service.ExpireDueReservationsAsync(stoppingToken);
                if (expired > 0)
                    _logger.LogInformation("Expired {Count} reservation(s).", expired);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown: stop looping without logging an error.
                break;
            }
            catch (Exception ex)
            {
                // One failed sweep must not kill the worker; retry next tick.
                _logger.LogError(ex, "Reservation expiration sweep failed; will retry next tick.");
            }
        }

        _logger.LogInformation("Reservation expiration worker stopping.");
    }
}
