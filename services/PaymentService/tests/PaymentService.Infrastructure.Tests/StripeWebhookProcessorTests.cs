using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.DTOs;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Tests;

public sealed class StripeWebhookProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenEventIsNew_ProcessesWebhookAndRecordsDedupRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var paymentService = new FakePaymentService();
        var processor = CreateProcessor(dbContext, paymentService);
        var webhookEvent = CreateWebhookEvent("evt_test_1");

        var result = await processor.ProcessAsync(webhookEvent, cancellationToken);

        Assert.Equal(paymentService.CannedResponse, result);

        var handled = Assert.Single(paymentService.HandledWebhooks);
        Assert.Equal(webhookEvent.StripeEventId, handled.StripeEventId);

        var dedupRow = await dbContext.ProcessedStripeEvents.SingleAsync(cancellationToken);
        Assert.Equal(webhookEvent.StripeEventId, dedupRow.StripeEventId);
    }

    [Fact]
    public async Task ProcessAsync_WhenEventWasAlreadyProcessed_DoesNotProcessAgain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var paymentService = new FakePaymentService();
        var processor = CreateProcessor(dbContext, paymentService);
        var webhookEvent = CreateWebhookEvent("evt_test_1");

        await processor.ProcessAsync(webhookEvent, cancellationToken);
        await processor.ProcessAsync(webhookEvent, cancellationToken);

        var handled = Assert.Single(paymentService.HandledWebhooks);
        Assert.Equal(webhookEvent.StripeEventId, handled.StripeEventId);

        var dedupRow = await dbContext.ProcessedStripeEvents.SingleAsync(cancellationToken);
        Assert.Equal(webhookEvent.StripeEventId, dedupRow.StripeEventId);
    }

    [Fact]
    public async Task ProcessAsync_WhenProcessingFails_DoesNotRecordDedupRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var paymentService = new FakePaymentService { FailProcessing = true };
        var processor = CreateProcessor(dbContext, paymentService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            processor.ProcessAsync(CreateWebhookEvent("evt_test_1"), cancellationToken));

        Assert.False(await dbContext.ProcessedStripeEvents.AnyAsync(cancellationToken));
    }

    [Fact]
    public async Task ProcessAsync_WhenWebhookIsForDifferentEvent_ProcessesBoth()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var paymentService = new FakePaymentService();
        var processor = CreateProcessor(dbContext, paymentService);

        await processor.ProcessAsync(CreateWebhookEvent("evt_test_1"), cancellationToken);
        await processor.ProcessAsync(CreateWebhookEvent("evt_test_2"), cancellationToken);

        Assert.Equal(2, paymentService.HandledWebhooks.Count);
        Assert.Equal(2, await dbContext.ProcessedStripeEvents.CountAsync(cancellationToken));
    }

    private static DbContextOptions<PaymentDbContext> CreateDbOptions(
        SqliteConnection connection) =>
        new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .Options;

    private static StripeWebhookProcessor CreateProcessor(
        PaymentDbContext dbContext,
        IPaymentService paymentService) =>
        new(dbContext, paymentService);

    private static StripeWebhookResult CreateWebhookEvent(string stripeEventId) =>
        new(
            StripeEventId: stripeEventId,
            StripePaymentIntentId: "pi_test_1",
            EventType: "payment_intent.succeeded",
            PaymentMethod: "card",
            FailureReason: null);

    private sealed class FakePaymentService : IPaymentService
    {
        public List<StripeWebhookResult> HandledWebhooks { get; } = [];
        public bool FailProcessing { get; init; }
        public PaymentResponse CannedResponse { get; } = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "pi_test",
            50m,
            "USD",
            "card",
            "Succeeded",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        public Task<PaymentResponse> InitiateAsync(
            InitiatePaymentRequest request,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

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
            CancellationToken ct = default)
        {
            if (FailProcessing)
                throw new InvalidOperationException("Webhook processing failed.");

            HandledWebhooks.Add(webhookEvent);
            return Task.FromResult(CannedResponse);
        }
    }
}
