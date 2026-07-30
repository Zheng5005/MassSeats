using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Messaging;

public sealed class SeatReservedConsumer : IEventConsumer<SeatReserved>
{
    private readonly PaymentDbContext _dbContext;
    private readonly IPaymentService _paymentService;
    private readonly PaymentOptions _options;

    public SeatReservedConsumer(
        PaymentDbContext dbContext,
        IPaymentService paymentService,
        IOptions<PaymentOptions> options)
    {
        _dbContext = dbContext;
        _paymentService = paymentService;
        _options = options.Value;
    }

    public async Task HandleAsync(
        SeatReserved @event,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        var processedOn = DateTimeOffset.UtcNow;
        var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO inbox_messages (message_id, type, processed_on)
            VALUES ({@event.Id}, {nameof(SeatReserved)}, {processedOn})
            ON CONFLICT (message_id) DO NOTHING
            """,
            cancellationToken);

        if (claimed == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await _paymentService.InitiateAsync(
            new InitiatePaymentRequest(
                @event.ReservationId,
                @event.Amount,
                _options.Currency),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
