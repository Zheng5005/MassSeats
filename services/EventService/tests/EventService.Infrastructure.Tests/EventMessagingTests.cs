using System.Text.Json;
using BuildingBlocks.Messaging.Contracts;
using EventService.Application.Services;
using EventService.Domain.Entities;
using EventService.Domain.Exceptions;
using EventService.Infrastructure.Messaging;
using EventService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.Tests;

public sealed class EventMessagingTests
{
    [Fact]
    public async Task SaveChanges_TranslatesEventDomainEventsIntoOutboxMessages()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection, new OutboxSaveChangesInterceptor());
        await using var dbContext = new EventDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var @event = await AddEventAsync(dbContext, clearDomainEvents: false, cancellationToken);

        @event.UpdateDetails(
            "Updated title",
            "Updated description",
            @event.CategoryId,
            @event.VenueId,
            DateTimeOffset.UtcNow.AddDays(2));
        await dbContext.SaveChangesAsync(cancellationToken);
        @event.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);

        var messages = await dbContext.OutboxMessages.ToListAsync(cancellationToken);
        Assert.Equal(3, messages.Count);
        Assert.Contains(messages, message => message.Type == nameof(EventCreated));
        Assert.Contains(messages, message => message.Type == nameof(EventUpdated));
        Assert.Contains(messages, message => message.Type == nameof(EventCancelled));
        Assert.All(messages, message => Assert.Null(message.ProcessedOn));

        var created = messages.Single(message => message.Type == nameof(EventCreated));
        using var content = JsonDocument.Parse(created.Content);
        Assert.Equal(@event.Id, content.RootElement.GetProperty("eventId").GetGuid());
    }

    [Fact]
    public async Task SeatReserved_DecrementsAvailabilityAndIsIdempotent()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        Guid eventId;

        await using (var setupContext = new EventDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync(cancellationToken);
            eventId = (await AddEventAsync(setupContext, true, cancellationToken)).Id;
        }

        var message = new SeatReserved(
            Guid.NewGuid(), eventId, Guid.NewGuid(), "Floor", "A", 1, 50m);
        await using (var firstContext = new EventDbContext(options))
        {
            await CreateSeatReservedConsumer(firstContext).HandleAsync(message, cancellationToken);
        }
        await using (var secondContext = new EventDbContext(options))
        {
            await CreateSeatReservedConsumer(secondContext).HandleAsync(message, cancellationToken);
        }

        await using var assertionContext = new EventDbContext(options);
        var stored = await assertionContext.Events.SingleAsync(cancellationToken);
        Assert.Equal(stored.TotalSeats - 1, stored.AvailableSeats);
        Assert.Equal(1, await assertionContext.InboxMessages.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task ReservationCancelled_ReleasesSeatAndRecordsInbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        await using var dbContext = new EventDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var @event = await AddEventAsync(dbContext, true, cancellationToken, reserveSeat: true);
        var consumer = new ReservationCancelledConsumer(
            dbContext,
            CreateEventService(dbContext));
        var message = new ReservationCancelled(
            Guid.NewGuid(), @event.Id, "Floor", "A", 1);

        await consumer.HandleAsync(message, cancellationToken);

        Assert.Equal(@event.TotalSeats, @event.AvailableSeats);
        Assert.Equal(message.Id, (await dbContext.InboxMessages.SingleAsync(cancellationToken)).MessageId);
    }

    [Fact]
    public async Task ReservationExpired_ReleasesSeatAndRecordsInbox()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        await using var dbContext = new EventDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var @event = await AddEventAsync(dbContext, true, cancellationToken, reserveSeat: true);
        var consumer = new ReservationExpiredConsumer(
            dbContext,
            CreateEventService(dbContext));
        var message = new ReservationExpired(
            Guid.NewGuid(), @event.Id, "Floor", "A", 1);

        await consumer.HandleAsync(message, cancellationToken);

        Assert.Equal(@event.TotalSeats, @event.AvailableSeats);
        Assert.Equal(message.Id, (await dbContext.InboxMessages.SingleAsync(cancellationToken)).MessageId);
    }

    [Fact]
    public async Task ReservationConfirmed_RecordsInboxWithoutChangingAvailability()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        await using var dbContext = new EventDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var @event = await AddEventAsync(dbContext, true, cancellationToken, reserveSeat: true);
        var message = new ReservationConfirmed(Guid.NewGuid(), @event.Id);

        await new ReservationConfirmedConsumer(dbContext)
            .HandleAsync(message, cancellationToken);

        Assert.Equal(@event.TotalSeats - 1, @event.AvailableSeats);
        Assert.Equal(message.Id, (await dbContext.InboxMessages.SingleAsync(cancellationToken)).MessageId);
    }

    [Fact]
    public async Task SeatReserved_WhenEventDoesNotExist_RollsBackInboxClaim()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var options = CreateDbOptions(connection);
        await using var dbContext = new EventDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        var consumer = CreateSeatReservedConsumer(dbContext);

        await Assert.ThrowsAsync<EventNotFoundException>(() =>
            consumer.HandleAsync(
                new SeatReserved(
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Floor", "A", 1, 50m),
                cancellationToken));

        Assert.False(await dbContext.InboxMessages.AnyAsync(cancellationToken));
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static DbContextOptions<EventDbContext> CreateDbOptions(
        SqliteConnection connection,
        OutboxSaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<EventDbContext>().UseSqlite(connection);
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return builder.Options;
    }

    private static async Task<Event> AddEventAsync(
        EventDbContext dbContext,
        bool clearDomainEvents,
        CancellationToken cancellationToken,
        bool reserveSeat = false)
    {
        var category = Category.Create($"Category {Guid.NewGuid():N}");
        var venue = Venue.Create(
            $"Venue {Guid.NewGuid():N}", "Address", "City", "Country", 100);
        var @event = Event.Create(
            $"Event {Guid.NewGuid():N}",
            null,
            category.Id,
            venue.Id,
            DateTimeOffset.UtcNow.AddDays(1),
            50m,
            10);

        if (reserveSeat)
            @event.DecrementAvailability();
        if (clearDomainEvents)
            @event.ClearDomainEvents();

        dbContext.AddRange(category, venue, @event);
        await dbContext.SaveChangesAsync(cancellationToken);
        return @event;
    }

    private static SeatReservedConsumer CreateSeatReservedConsumer(EventDbContext dbContext) =>
        new(dbContext, CreateEventService(dbContext));

    private static EventAppService CreateEventService(EventDbContext dbContext) =>
        new(new EventRepository(dbContext));
}
