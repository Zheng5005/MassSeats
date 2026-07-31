using BookingService.Application;
using BookingService.Application.Services;
using BookingService.Domain.Entities;
using BookingService.Domain.Enums;
using BookingService.Domain.Exceptions;
using BookingService.Infrastructure.Messaging;
using BookingService.Infrastructure.Persistence;
using BuildingBlocks.Messaging.Contracts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BookingService.Infrastructure.Tests;

public sealed class PaymentConsumersTests
{
    [Fact]
    public async Task PaymentSucceeded_ConfirmsReservationAndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        var reservation = await CreateReservationAsync(options, cancellationToken);
        var @event = new PaymentSucceeded(Guid.NewGuid(), reservation.Id);

        await using (var firstContext = new BookingDbContext(options))
        {
            await CreateSucceededConsumer(firstContext)
                .HandleAsync(@event, cancellationToken);
        }

        await using (var secondContext = new BookingDbContext(options))
        {
            await CreateSucceededConsumer(secondContext)
                .HandleAsync(@event, cancellationToken);
        }

        await using var assertionContext = new BookingDbContext(options);
        var stored = await assertionContext.Reservations.SingleAsync(cancellationToken);
        var inbox = await assertionContext.InboxMessages.SingleAsync(cancellationToken);
        Assert.Equal(ReservationStatus.Confirmed, stored.Status);
        Assert.Equal(@event.PaymentId, stored.PaymentId);
        Assert.Equal(@event.Id, inbox.MessageId);
        Assert.Equal(nameof(PaymentSucceeded), inbox.Type);
    }

    [Fact]
    public async Task PaymentFailed_CancelsReservationAndRecordsInbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        var reservation = await CreateReservationAsync(options, cancellationToken);
        var @event = new PaymentFailed(Guid.NewGuid(), reservation.Id, "Card declined.");

        await using (var dbContext = new BookingDbContext(options))
        {
            await CreateFailedConsumer(dbContext)
                .HandleAsync(@event, cancellationToken);
        }

        await using var assertionContext = new BookingDbContext(options);
        var stored = await assertionContext.Reservations.SingleAsync(cancellationToken);
        var inbox = await assertionContext.InboxMessages.SingleAsync(cancellationToken);
        Assert.Equal(ReservationStatus.Cancelled, stored.Status);
        Assert.Equal(@event.Id, inbox.MessageId);
        Assert.Equal(nameof(PaymentFailed), inbox.Type);
    }

    [Fact]
    public async Task PaymentSucceeded_WhenReservationDoesNotExist_RollsBackInboxClaim()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);

        await using var dbContext = new BookingDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var consumer = CreateSucceededConsumer(dbContext);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() =>
            consumer.HandleAsync(
                new PaymentSucceeded(Guid.NewGuid(), Guid.NewGuid()),
                cancellationToken));

        Assert.False(await dbContext.InboxMessages.AnyAsync(cancellationToken));
    }

    [Fact]
    public async Task SaveChanges_WhenTerminalTransitionsRace_OnlyFirstTransitionCommits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        var reservation = await CreateReservationAsync(options, cancellationToken);

        await using var confirmingContext = new BookingDbContext(options);
        await using var cancellingContext = new BookingDbContext(options);
        var confirmingReservation = await confirmingContext.Reservations
            .SingleAsync(item => item.Id == reservation.Id, cancellationToken);
        var cancellingReservation = await cancellingContext.Reservations
            .SingleAsync(item => item.Id == reservation.Id, cancellationToken);

        confirmingReservation.Confirm(Guid.NewGuid());
        cancellingReservation.Cancel();
        await confirmingContext.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            cancellingContext.SaveChangesAsync(cancellationToken));

        await using var assertionContext = new BookingDbContext(options);
        var stored = await assertionContext.Reservations.SingleAsync(cancellationToken);
        Assert.Equal(ReservationStatus.Confirmed, stored.Status);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static DbContextOptions<BookingDbContext> CreateDbOptions(
        SqliteConnection connection) =>
        new DbContextOptionsBuilder<BookingDbContext>()
            .UseSqlite(connection)
            .Options;

    private static async Task<Reservation> CreateReservationAsync(
        DbContextOptions<BookingDbContext> options,
        CancellationToken cancellationToken)
    {
        await using var dbContext = new BookingDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var reservation = Reservation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Floor",
            "A",
            1,
            50m,
            TimeSpan.FromMinutes(10));
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return reservation;
    }

    private static PaymentSucceededConsumer CreateSucceededConsumer(
        BookingDbContext dbContext) =>
        new(dbContext, CreateReservationService(dbContext));

    private static PaymentFailedConsumer CreateFailedConsumer(
        BookingDbContext dbContext) =>
        new(dbContext, CreateReservationService(dbContext));

    private static ReservationAppService CreateReservationService(
        BookingDbContext dbContext) =>
        new(new ReservationRepository(dbContext), new ReservationOptions());
}
