using BuildingBlocks.Messaging.Abstractions;
using BuildingBlocks.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PaymentService.Application;
using PaymentService.Application.Interfaces;
using PaymentService.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace PaymentService.Infrastructure.Tests;

public sealed class SeatReservedMessagingTests
{
    [Fact]
    public async Task SeatReserved_CreatesPaymentAndRecordsInbox()
    {
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("PAYMENT_INTEGRATION_TESTS") == "true",
            "Set PAYMENT_INTEGRATION_TESTS=true to run Payment messaging integration tests.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var rabbitPort = GetPort("RABBITMQ_PORT", 5672);
        var queueName = $"payment.phase3.{Guid.NewGuid():N}.queue";
        var gateway = new FakePaymentGateway();
        var builder = Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PaymentDb"] = Environment.GetEnvironmentVariable("PAYMENT_DB")
                ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=paymentservice_phase3",
            ["Stripe:SecretKey"] = "sk_test_not_used",
            ["Stripe:WebhookSecret"] = "whsec_not_used",
            ["Payment:Currency"] = "USD",
            ["RabbitMq:Host"] = "localhost",
            ["RabbitMq:Port"] = rabbitPort.ToString(),
            ["RabbitMq:UserName"] = "guest",
            ["RabbitMq:Password"] = "guest",
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:QueueName"] = queueName
        });

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddSingleton(gateway);
        builder.Services.AddScoped<IPaymentGateway>(provider =>
            provider.GetRequiredService<FakePaymentGateway>());

        using var host = builder.Build();
        await MigrateDatabaseAsync(host.Services, cancellationToken);

        try
        {
            await host.StartAsync(cancellationToken);
            var @event = CreateEvent();
            var publisher = host.Services.GetRequiredService<IEventPublisher>();

            await publisher.PublishAsync(@event, cancellationToken);
            await WaitForPaymentAsync(host.Services, @event, cancellationToken);

            Assert.Equal(1, gateway.CreateCalls);
            Assert.Equal(@event.ReservationId, gateway.BookingId);
        }
        finally
        {
            await host.StopAsync(cancellationToken);
            await DeleteQueuesAsync(rabbitPort, queueName, cancellationToken);
        }
    }

    private static async Task MigrateDatabaseAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static async Task WaitForPaymentAsync(
        IServiceProvider services,
        SeatReserved @event,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();

            var paymentExists = await dbContext.Payments
                .AnyAsync(payment => payment.BookingId == @event.ReservationId, cancellationToken);
            var inboxExists = await dbContext.InboxMessages
                .AnyAsync(message => message.MessageId == @event.Id, cancellationToken);

            if (paymentExists && inboxExists)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException("Payment and Inbox records were not committed within 10 seconds.");
    }

    private static async Task DeleteQueuesAsync(
        int rabbitPort,
        string queueName,
        CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            Port = rabbitPort,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.QueueDeleteAsync(queueName, cancellationToken: cancellationToken);
        await channel.QueueDeleteAsync(
            queueName.Replace(".queue", ".dlq", StringComparison.Ordinal),
            cancellationToken: cancellationToken);
    }

    private static int GetPort(string environmentVariable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(environmentVariable), out var port)
            ? port
            : fallback;

    private static SeatReserved CreateEvent() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Floor",
            "A",
            1,
            75m);

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        private int _createCalls;

        public int CreateCalls => _createCalls;
        public Guid BookingId { get; private set; }

        public Task<string> CreatePaymentIntentAsync(
            Guid bookingId,
            decimal amount,
            string currency,
            CancellationToken ct = default)
        {
            BookingId = bookingId;
            Interlocked.Increment(ref _createCalls);
            return Task.FromResult($"pi_test_{Guid.NewGuid():N}");
        }

        public Task<StripeWebhookResult?> VerifyWebhookAsync(
            string rawBody,
            string signatureHeader,
            CancellationToken ct = default) =>
            Task.FromResult<StripeWebhookResult?>(null);
    }
}
