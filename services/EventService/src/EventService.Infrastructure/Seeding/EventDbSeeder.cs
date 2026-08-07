using EventService.Domain.Entities;
using EventService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EventService.Infrastructure.Seeding;

/// <summary>
/// Seeds the demo catalog (categories, venues and events) on startup.
/// Guarded three ways so it never runs outside Development or against
/// non-empty tables.
/// </summary>
public sealed class EventDbSeeder
{
    private const string EnabledKey = "SeedData:Enabled";

    private readonly EventDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EventDbSeeder> _logger;

    public EventDbSeeder(
        EventDbContext context,
        IConfiguration configuration,
        ILogger<EventDbSeeder> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!IsEnabled())
            return;

        var categories = await SeedCategoriesAsync(ct);
        var venues = await SeedVenuesAsync(ct);

        await _context.SaveChangesAsync(ct);

        if (await _context.Events.AnyAsync(ct))
            return;

        var concert = await ResolveCategoryAsync(categories, "Concert", ct);
        var theater = await ResolveCategoryAsync(categories, "Theater", ct);
        var sports = await ResolveCategoryAsync(categories, "Sports", ct);

        var grandHall = await ResolveVenueAsync(venues, "Grand Hall", ct);
        var openAirArena = await ResolveVenueAsync(venues, "Open Air Arena", ct);

        var now = DateTimeOffset.UtcNow;

        var events = new[]
        {
            Event.Create(
                title: "Summer Symphony",
                description: "An evening of classical favorites with the city orchestra.",
                categoryId: concert.Id,
                venueId: grandHall.Id,
                eventDate: now.AddDays(14),
                ticketPrice: 45.00m,
                totalSeats: 500),
            Event.Create(
                title: "The Glass Menagerie",
                description: "Tennessee Williams' classic family drama.",
                categoryId: theater.Id,
                venueId: grandHall.Id,
                eventDate: now.AddDays(21),
                ticketPrice: 30.00m,
                totalSeats: 200),
            Event.Create(
                title: "City Cup Final",
                description: "The season's decisive football championship match.",
                categoryId: sports.Id,
                venueId: openAirArena.Id,
                eventDate: now.AddDays(30),
                ticketPrice: 60.00m,
                totalSeats: 2000),
        };

        await _context.Events.AddRangeAsync(events, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {CategoryCount} categories, {VenueCount} venues and {EventCount} events.",
            categories.Count,
            venues.Count,
            events.Length);
    }

    private async Task<List<Category>> SeedCategoriesAsync(CancellationToken ct)
    {
        if (await _context.Categories.AnyAsync(ct))
            return [];

        var categories = new List<Category>
        {
            Category.Create("Concert", "Live music performances"),
            Category.Create("Theater", "Plays, musicals and stage shows"),
            Category.Create("Sports", "Sporting events and matches"),
        };

        await _context.Categories.AddRangeAsync(categories, ct);
        return categories;
    }

    private async Task<List<Venue>> SeedVenuesAsync(CancellationToken ct)
    {
        if (await _context.Venues.AnyAsync(ct))
            return [];

        var venues = new List<Venue>
        {
            Venue.Create("Grand Hall", "100 Main St", "Springfield", "US", 500),
            Venue.Create("Open Air Arena", "250 Riverside Dr", "Springfield", "US", 2000),
        };

        await _context.Venues.AddRangeAsync(venues, ct);
        return venues;
    }

    private async Task<Category> ResolveCategoryAsync(
        IReadOnlyList<Category> created,
        string name,
        CancellationToken ct)
    {
        var category = created.FirstOrDefault(c => c.Name == name);
        return category ?? await _context.Categories.SingleAsync(c => c.Name == name, ct);
    }

    private async Task<Venue> ResolveVenueAsync(
        IReadOnlyList<Venue> created,
        string name,
        CancellationToken ct)
    {
        var venue = created.FirstOrDefault(v => v.Name == name);
        return venue ?? await _context.Venues.SingleAsync(v => v.Name == name, ct);
    }

    private bool IsEnabled() =>
        string.Equals(
            _configuration[EnabledKey],
            "true",
            StringComparison.OrdinalIgnoreCase);
}
