using BuildingBlocks.Domain;
using EventService.Domain.Events;
using EventService.Domain.Exceptions;

namespace EventService.Domain.Entities;

/// <summary>
/// Aggregate root that represents an event on the platform.
/// State changes go through behavior methods so the entity always
/// protects its own invariants (no anemic model).
/// </summary>
public class Event : AggregateRoot
{
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid VenueId { get; private set; }
    public DateTimeOffset EventDate { get; private set; }
    public decimal TicketPrice { get; private set; }
    public int TotalSeats { get; private set; }
    public int AvailableSeats { get; private set; }
    public string? BannerImage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Required by EF Core to materialize entities; not for application code.
    private Event()
    {
        Title = null!;
    }

    private Event(
        Guid id,
        string title,
        string? description,
        Guid categoryId,
        Guid venueId,
        DateTimeOffset eventDate,
        decimal ticketPrice,
        int totalSeats,
        string? bannerImage,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : base(id)
    {
        Title = title;
        Description = description;
        CategoryId = categoryId;
        VenueId = venueId;
        EventDate = eventDate;
        TicketPrice = ticketPrice;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
        BannerImage = bannerImage;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory that creates a valid event.
    /// </summary>
    public static Event Create(
        string title,
        string? description,
        Guid categoryId,
        Guid venueId,
        DateTimeOffset eventDate,
        decimal ticketPrice,
        int totalSeats,
        string? bannerImage = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainValidationException("Title is required.");
        if (ticketPrice < 0)
            throw new DomainValidationException("Ticket price cannot be negative.");
        if (totalSeats <= 0)
            throw new DomainValidationException("Total seats must be greater than zero.");

        var now = DateTimeOffset.UtcNow;

        if (eventDate <= now)
            throw new EventDateInThePastException(eventDate);

        var @event = new Event(
            id: Guid.NewGuid(),
            title: title.Trim(),
            description: description?.Trim(),
            categoryId: categoryId,
            venueId: venueId,
            eventDate: eventDate,
            ticketPrice: ticketPrice,
            totalSeats: totalSeats,
            bannerImage: bannerImage?.Trim(),
            createdAt: now,
            updatedAt: now);

        @event.RaiseDomainEvent(new EventCreatedDomainEvent(
            @event.Id,
            @event.Title,
            @event.VenueId,
            @event.CategoryId,
            @event.EventDate,
            @event.TotalSeats));

        return @event;
    }

    public void UpdateDetails(string title, string? description, Guid categoryId, Guid venueId, DateTimeOffset eventDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainValidationException("Title is required.");
        if (eventDate <= DateTimeOffset.UtcNow)
            throw new EventDateInThePastException(eventDate);

        Title = title.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        VenueId = venueId;
        EventDate = eventDate;
        Touch();

        RaiseDomainEvent(new EventUpdatedDomainEvent(Id, Title, EventDate, TotalSeats));
    }

    public void UpdatePricing(decimal ticketPrice)
    {
        if (ticketPrice < 0)
            throw new DomainValidationException("Ticket price cannot be negative.");

        TicketPrice = ticketPrice;
        Touch();
    }

    public void UpdateBanner(string? bannerImage)
    {
        BannerImage = bannerImage?.Trim();
        Touch();
    }

    public void DecrementAvailability()
    {
        if (AvailableSeats <= 0)
            throw new DomainValidationException("No seats are available for this event.");

        AvailableSeats--;
        Touch();
    }

    public void ReleaseSeat()
    {
        if (AvailableSeats >= TotalSeats)
            throw new DomainValidationException("Available seats cannot exceed total seats.");

        AvailableSeats++;
        Touch();
    }

    public void Cancel() => RaiseDomainEvent(new EventCancelledDomainEvent(Id));

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
