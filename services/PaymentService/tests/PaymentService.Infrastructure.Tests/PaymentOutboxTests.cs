using System.Text.Json;
using BuildingBlocks.Messaging.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Tests;

public sealed class PaymentOutboxTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_WhenPaymentCompletes_WritesTerminalEventToOutbox(bool succeeds)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new OutboxSaveChangesInterceptor())
            .Options;

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var payment = Payment.Create(
            Guid.NewGuid(),
            $"pi_test_{Guid.NewGuid():N}",
            50m,
            "USD");
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        Assert.False(await dbContext.OutboxMessages.AnyAsync(cancellationToken));

        if (succeeds)
            payment.Succeed("card");
        else
            payment.Fail("Card declined.");

        await dbContext.SaveChangesAsync(cancellationToken);

        var message = await dbContext.OutboxMessages.SingleAsync(cancellationToken);
        Assert.Null(message.ProcessedOn);
        Assert.Equal(0, message.Attempts);

        if (succeeds)
        {
            Assert.Equal(nameof(PaymentSucceeded), message.Type);
            var @event = Deserialize<PaymentSucceeded>(message.Content);
            Assert.Equal(payment.Id, @event.PaymentId);
            Assert.Equal(payment.BookingId, @event.BookingId);
        }
        else
        {
            Assert.Equal(nameof(PaymentFailed), message.Type);
            var @event = Deserialize<PaymentFailed>(message.Content);
            Assert.Equal(payment.Id, @event.PaymentId);
            Assert.Equal(payment.BookingId, @event.BookingId);
            Assert.Equal("Card declined.", @event.Reason);
        }
    }

    [Fact]
    public async Task SaveChanges_WhenTerminalTransitionsRace_OnlyFirstOutboxEventCommits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new OutboxSaveChangesInterceptor())
            .Options;

        Guid paymentId;
        await using (var setupContext = new PaymentDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(cancellationToken);
            var payment = Payment.Create(
                Guid.NewGuid(),
                $"pi_test_{Guid.NewGuid():N}",
                50m,
                "USD");
            setupContext.Payments.Add(payment);
            await setupContext.SaveChangesAsync(cancellationToken);
            paymentId = payment.Id;
        }

        await using var succeedingContext = new PaymentDbContext(options);
        await using var failingContext = new PaymentDbContext(options);
        var succeedingPayment = await succeedingContext.Payments
            .SingleAsync(payment => payment.Id == paymentId, cancellationToken);
        var failingPayment = await failingContext.Payments
            .SingleAsync(payment => payment.Id == paymentId, cancellationToken);

        succeedingPayment.Succeed("card");
        failingPayment.Fail("Card declined.");
        await succeedingContext.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            failingContext.SaveChangesAsync(cancellationToken));

        await using var assertionContext = new PaymentDbContext(options);
        var message = await assertionContext.OutboxMessages.SingleAsync(cancellationToken);
        Assert.Equal(nameof(PaymentSucceeded), message.Type);
    }

    [Fact]
    public async Task SaveChanges_WhenPaymentFails_PersistsFailureReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(new OutboxSaveChangesInterceptor())
            .Options;

        Guid paymentId;
        await using (var setupContext = new PaymentDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(cancellationToken);
            var payment = Payment.Create(
                Guid.NewGuid(),
                $"pi_test_{Guid.NewGuid():N}",
                50m,
                "USD");
            setupContext.Payments.Add(payment);
            await setupContext.SaveChangesAsync(cancellationToken);
            paymentId = payment.Id;
        }

        await using (var failingContext = new PaymentDbContext(options))
        {
            var payment = await failingContext.Payments
                .SingleAsync(item => item.Id == paymentId, cancellationToken);
            payment.Fail("Card declined.");
            await failingContext.SaveChangesAsync(cancellationToken);
        }

        await using var assertionContext = new PaymentDbContext(options);
        var stored = await assertionContext.Payments
            .SingleAsync(item => item.Id == paymentId, cancellationToken);
        Assert.Equal("Card declined.", stored.FailureReason);
    }

    [Fact]
    public async Task SaveChanges_WhenPaymentSucceeds_DoesNotPersistFailureReason()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new PaymentDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var payment = Payment.Create(
            Guid.NewGuid(),
            $"pi_test_{Guid.NewGuid():N}",
            50m,
            "USD");
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);
        payment.Succeed("card");
        await dbContext.SaveChangesAsync(cancellationToken);

        Assert.Null(payment.FailureReason);
    }

    private static TEvent Deserialize<TEvent>(string content) =>
        JsonSerializer.Deserialize<TEvent>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new JsonException($"Could not deserialize {typeof(TEvent).Name}.");
}
