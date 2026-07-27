using EventService.Domain.Entities;
using EventService.Domain.Events;
using EventService.Domain.Exceptions;

namespace EventService.Domain.Tests;

public sealed class EventTests
{
    [Fact]
    public void Create_InitializesAvailabilityAndRaisesCreatedEvent()
    {
        var @event = CreateEvent(totalSeats: 2);

        Assert.Equal(2, @event.TotalSeats);
        Assert.Equal(2, @event.AvailableSeats);

        var domainEvent = Assert.Single(@event.DomainEvents);
        var created = Assert.IsType<EventCreatedDomainEvent>(domainEvent);
        Assert.Equal(@event.Id, created.EventId);
        Assert.Equal(@event.Title, created.Title);
        Assert.Equal(@event.TotalSeats, created.TotalSeats);
    }

    [Fact]
    public void DecrementAvailability_WhenSeatExists_DecrementsAvailableSeats()
    {
        var @event = CreateEvent(totalSeats: 2);

        @event.DecrementAvailability();

        Assert.Equal(1, @event.AvailableSeats);
    }

    [Fact]
    public void DecrementAvailability_WhenSoldOut_Throws()
    {
        var @event = CreateEvent(totalSeats: 1);
        @event.DecrementAvailability();

        var exception = Assert.Throws<DomainValidationException>(
            @event.DecrementAvailability);

        Assert.Equal("No seats are available for this event.", exception.Message);
        Assert.Equal(0, @event.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeat_WhenReservationExists_IncrementsAvailableSeats()
    {
        var @event = CreateEvent(totalSeats: 2);
        @event.DecrementAvailability();

        @event.ReleaseSeat();

        Assert.Equal(2, @event.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeat_WhenAllSeatsAvailable_Throws()
    {
        var @event = CreateEvent(totalSeats: 1);

        var exception = Assert.Throws<DomainValidationException>(@event.ReleaseSeat);

        Assert.Equal("Available seats cannot exceed total seats.", exception.Message);
        Assert.Equal(1, @event.AvailableSeats);
    }

    [Fact]
    public void UpdateDetails_RaisesUpdatedEventWithCurrentCatalogData()
    {
        var @event = CreateEvent(totalSeats: 3);
        @event.ClearDomainEvents();
        var updatedDate = DateTimeOffset.UtcNow.AddDays(2);

        @event.UpdateDetails(
            "Updated event",
            "Updated description",
            Guid.NewGuid(),
            Guid.NewGuid(),
            updatedDate);

        var domainEvent = Assert.Single(@event.DomainEvents);
        var updated = Assert.IsType<EventUpdatedDomainEvent>(domainEvent);
        Assert.Equal(@event.Id, updated.EventId);
        Assert.Equal("Updated event", updated.Title);
        Assert.Equal(updatedDate, updated.EventDate);
        Assert.Equal(3, updated.TotalSeats);
    }

    [Fact]
    public void Cancel_RaisesCancelledEvent()
    {
        var @event = CreateEvent();
        @event.ClearDomainEvents();

        @event.Cancel();

        var domainEvent = Assert.Single(@event.DomainEvents);
        var cancelled = Assert.IsType<EventCancelledDomainEvent>(domainEvent);
        Assert.Equal(@event.Id, cancelled.EventId);
    }

    private static Event CreateEvent(int totalSeats = 10) =>
        Event.Create(
            "Test event",
            "Test description",
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            25m,
            totalSeats);
}
