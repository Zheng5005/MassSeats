using System.Text.Json;
using BookingService.Infrastructure.Persistence;
using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookingService.Infrastructure.Messaging;

public sealed class OutboxPublisherWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxPublisherWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox publishing cycle failed; will retry.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await context.OutboxMessages
            .Where(message => message.ProcessedOn == null)
            .OrderBy(message => message.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await PublishAsync(publisher, message, cancellationToken);
                message.MarkProcessed(DateTimeOffset.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                message.RecordFailure(exception.Message);
                _logger.LogError(
                    exception,
                    "Failed to publish outbox message {MessageId} of type {MessageType}; will retry.",
                    message.Id,
                    message.Type);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static Task PublishAsync(
        IEventPublisher publisher,
        OutboxMessage message,
        CancellationToken cancellationToken) => message.Type switch
        {
            nameof(SeatReserved) => publisher.PublishAsync(
                Deserialize<SeatReserved>(message), cancellationToken),
            nameof(ReservationConfirmed) => publisher.PublishAsync(
                Deserialize<ReservationConfirmed>(message), cancellationToken),
            nameof(ReservationCancelled) => publisher.PublishAsync(
                Deserialize<ReservationCancelled>(message), cancellationToken),
            nameof(ReservationExpired) => publisher.PublishAsync(
                Deserialize<ReservationExpired>(message), cancellationToken),
            _ => throw new InvalidOperationException(
                $"Outbox message type '{message.Type}' is not supported by BookingService.")
        };

    private static TEvent Deserialize<TEvent>(OutboxMessage message) =>
        JsonSerializer.Deserialize<TEvent>(message.Content, SerializerOptions)
        ?? throw new JsonException($"Could not deserialize outbox message {message.Id} as {message.Type}.");
}
