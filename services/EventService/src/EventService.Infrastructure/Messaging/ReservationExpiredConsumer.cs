using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using EventService.Application.Interfaces;
using EventService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.Messaging;

public sealed class ReservationExpiredConsumer : IEventConsumer<ReservationExpired>
{
    private readonly EventDbContext _dbContext;
    private readonly IEventService _eventService;

    public ReservationExpiredConsumer(EventDbContext dbContext, IEventService eventService)
    {
        _dbContext = dbContext;
        _eventService = eventService;
    }

    public async Task HandleAsync(
        ReservationExpired @event,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages (message_id, type, processed_on)
            VALUES ({@event.Id}, {nameof(ReservationExpired)}, {DateTimeOffset.UtcNow})
            ON CONFLICT (message_id) DO NOTHING
            """,
            cancellationToken);

        if (claimed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await _eventService.ReleaseSeatAsync(@event.EventId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
