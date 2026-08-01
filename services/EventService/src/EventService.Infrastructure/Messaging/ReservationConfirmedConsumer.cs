using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using EventService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.Messaging;

public sealed class ReservationConfirmedConsumer : IEventConsumer<ReservationConfirmed>
{
    private readonly EventDbContext _dbContext;

    public ReservationConfirmedConsumer(EventDbContext dbContext) => _dbContext = dbContext;

    public async Task HandleAsync(
        ReservationConfirmed @event,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages (message_id, type, processed_on)
            VALUES ({@event.Id}, {nameof(ReservationConfirmed)}, {DateTimeOffset.UtcNow})
            ON CONFLICT (message_id) DO NOTHING
            """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
