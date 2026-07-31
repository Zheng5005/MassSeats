using BookingService.Application.DTOs;
using BookingService.Application.Interfaces;
using BookingService.Infrastructure.Persistence;
using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Messaging;

public sealed class PaymentSucceededConsumer : IEventConsumer<PaymentSucceeded>
{
    private readonly BookingDbContext _dbContext;
    private readonly IReservationService _reservationService;

    public PaymentSucceededConsumer(
        BookingDbContext dbContext,
        IReservationService reservationService)
    {
        _dbContext = dbContext;
        _reservationService = reservationService;
    }

    public async Task HandleAsync(
        PaymentSucceeded @event,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var processedOn = DateTimeOffset.UtcNow;
        var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages (message_id, type, processed_on)
            VALUES ({@event.Id}, {nameof(PaymentSucceeded)}, {processedOn})
            ON CONFLICT (message_id) DO NOTHING
            """,
            cancellationToken);

        if (claimed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await _reservationService.ConfirmAsync(
            @event.BookingId,
            new ConfirmReservationRequest(@event.PaymentId),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
