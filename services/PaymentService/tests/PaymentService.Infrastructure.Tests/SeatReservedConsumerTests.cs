using BuildingBlocks.Messaging.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Configuration;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Tests;

public sealed class SeatReservedConsumerTests
{
    [Fact]
    public async Task HandleAsync_WhenMessageIsNew_InitiatesPaymentAndRecordsInbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var paymentService = new FakePaymentService();
        var consumer = CreateConsumer(dbContext, paymentService);
        var @event = CreateEvent();

        await consumer.HandleAsync(@event, cancellationToken);

        var request = Assert.Single(paymentService.Requests);
        Assert.Equal(@event.ReservationId, request.BookingId);
        Assert.Equal(@event.Amount, request.Amount);
        Assert.Equal("USD", request.Currency);

        var inboxMessage = await dbContext.InboxMessages.SingleAsync(cancellationToken);
        Assert.Equal(@event.Id, inboxMessage.MessageId);
        Assert.Equal(nameof(SeatReserved), inboxMessage.Type);
    }

    [Fact]
    public async Task HandleAsync_WhenMessageWasProcessed_DoesNotInitiatePaymentAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        var paymentService = new FakePaymentService();
        var @event = CreateEvent();

        await using (var firstContext = new PaymentDbContext(options))
        {
            await firstContext.Database.EnsureCreatedAsync(cancellationToken);
            await CreateConsumer(firstContext, paymentService)
                .HandleAsync(@event, cancellationToken);
        }

        await using (var secondContext = new PaymentDbContext(options))
        {
            await CreateConsumer(secondContext, paymentService)
                .HandleAsync(@event, cancellationToken);
        }

        Assert.Single(paymentService.Requests);
    }

    [Fact]
    public async Task HandleAsync_WhenPaymentInitiationFails_DoesNotRecordInbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var paymentService = new FakePaymentService { FailInitiation = true };
        var consumer = CreateConsumer(dbContext, paymentService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.HandleAsync(CreateEvent(), cancellationToken));

        Assert.False(await dbContext.InboxMessages.AnyAsync(cancellationToken));
    }

    private static DbContextOptions<PaymentDbContext> CreateDbOptions(
        SqliteConnection connection) =>
        new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .Options;

    private static SeatReservedConsumer CreateConsumer(
        PaymentDbContext dbContext,
        IPaymentService paymentService) =>
        new(
            dbContext,
            paymentService,
            Options.Create(new PaymentOptions { Currency = "USD" }));

    private static SeatReserved CreateEvent() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Floor",
            "A",
            1,
            50m);

    private sealed class FakePaymentService : IPaymentService
    {
        public List<InitiatePaymentRequest> Requests { get; } = [];
        public bool FailInitiation { get; init; }

        public Task<PaymentResponse> InitiateAsync(
            InitiatePaymentRequest request,
            CancellationToken ct = default)
        {
            if (FailInitiation)
                throw new InvalidOperationException("Payment initiation failed.");

            Requests.Add(request);
            return Task.FromResult(new PaymentResponse(
                Guid.NewGuid(),
                request.BookingId,
                "pi_test",
                request.Amount,
                request.Currency,
                null,
                "Pending",
                DateTimeOffset.UtcNow,
                null,
                null));
        }

        public Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PaymentResponse?> GetByBookingIdAsync(
            Guid bookingId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<string?> GetClientSecretAsync(
            Guid bookingId,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<PaymentResponse> HandleWebhookAsync(
            StripeWebhookResult webhookEvent,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
