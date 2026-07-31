using BookingService.Application.Interfaces;
using BookingService.Infrastructure.Persistence;
using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Messaging;

public sealed class PaymentFailedConsumer : IEventConsumer<PaymentFailed>
{
    private readonly BookingDbContext _dbContext;
    private readonly IReservationService _reservationService;

    public PaymentFailedConsumer(
        BookingDbContext dbContext,
        IReservationService reservationService)
    {
        _dbContext = dbContext;
        _reservationService = reservationService;
    }

    public async Task HandleAsync(
        PaymentFailed @event,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var processedOn = DateTimeOffset.UtcNow;
        var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages (message_id, type, processed_on)
            VALUES ({@event.Id}, {nameof(PaymentFailed)}, {processedOn})
            ON CONFLICT (message_id) DO NOTHING
            """,
            cancellationToken);

        if (claimed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await _reservationService.CancelAsync(@event.BookingId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
