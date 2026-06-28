namespace EventService.Domain.Entities;

/// <summary>
/// Aggregate root that represents an event on the platform.
/// State changes go through behavior methods so the entity always
/// protects its own invariants (no anemic model).
/// </summary>
public class Event
{
    public Guid Id { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid VenueId { get; private set; }
    public DateTimeOffset EventDate { get; private set; }
    public float TicketPrice { get; private set; }
    public int TotalSeats { get; private set; }
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
        float ticketPrice,
        int totalSeats,
        string? bannerImage,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Title = title;
        Description = description;
        CategoryId = categoryId;
        VenueId = venueId;
        EventDate = eventDate;
        TicketPrice = ticketPrice;
        TotalSeats = totalSeats;
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
        float ticketPrice,
        int totalSeats,
        string? bannerImage = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (ticketPrice < 0)
            throw new ArgumentException("Ticket price cannot be negative.", nameof(ticketPrice));
        if (totalSeats <= 0)
            throw new ArgumentException("Total seats must be greater than zero.", nameof(totalSeats));

        var now = DateTimeOffset.UtcNow;

        return new Event(
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
    }

    public void UpdateDetails(string title, string? description, Guid categoryId, Guid venueId, DateTimeOffset eventDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        Title = title.Trim();
        Description = description?.Trim();
        CategoryId = categoryId;
        VenueId = venueId;
        EventDate = eventDate;
        Touch();
    }

    public void UpdatePricing(float ticketPrice)
    {
        if (ticketPrice < 0)
            throw new ArgumentException("Ticket price cannot be negative.", nameof(ticketPrice));

        TicketPrice = ticketPrice;
        Touch();
    }

    public void UpdateBanner(string? bannerImage)
    {
        BannerImage = bannerImage?.Trim();
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
