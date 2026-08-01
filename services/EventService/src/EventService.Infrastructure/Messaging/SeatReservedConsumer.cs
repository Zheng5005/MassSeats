using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using EventService.Application.Interfaces;
using EventService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.Messaging;

public sealed class SeatReservedConsumer : IEventConsumer<SeatReserved>
{
    private readonly EventDbContext _dbContext;
    private readonly IEventService _eventService;

    public SeatReservedConsumer(EventDbContext dbContext, IEventService eventService)
    {
        _dbContext = dbContext;
        _eventService = eventService;
    }

    public async Task HandleAsync(SeatReserved @event, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var claimed = await ClaimAsync(@event.Id, nameof(SeatReserved), cancellationToken);

        if (claimed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await _eventService.DecrementAvailabilityAsync(@event.EventId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<int> ClaimAsync(Guid messageId, string type, CancellationToken cancellationToken) =>
        _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages (message_id, type, processed_on)
            VALUES ({messageId}, {type}, {DateTimeOffset.UtcNow})
            ON CONFLICT (message_id) DO NOTHING
            """,
            cancellationToken);
}
