namespace EventService.Domain.Entities;

/// <summary>
/// Entity that represents a venue where events take place.
/// </summary>
public class Venue
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Address { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }
    public int Capacity { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Required by EF Core to materialize entities; not for application code.
    private Venue()
    {
        Name = null!;
        Address = null!;
        City = null!;
        Country = null!;
    }

    private Venue(
        Guid id,
        string name,
        string address,
        string city,
        string country,
        int capacity,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        Address = address;
        City = city;
        Country = country;
        Capacity = capacity;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory that creates a valid venue.
    /// </summary>
    public static Venue Create(
        string name,
        string address,
        string city,
        string country,
        int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country is required.", nameof(country));
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));

        var now = DateTimeOffset.UtcNow;

        return new Venue(
            id: Guid.NewGuid(),
            name: name.Trim(),
            address: address.Trim(),
            city: city.Trim(),
            country: country.Trim(),
            capacity: capacity,
            createdAt: now,
            updatedAt: now);
    }

    public void Update(string name, string address, string city, string country, int capacity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country is required.", nameof(country));
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));

        Name = name.Trim();
        Address = address.Trim();
        City = city.Trim();
        Country = country.Trim();
        Capacity = capacity;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
