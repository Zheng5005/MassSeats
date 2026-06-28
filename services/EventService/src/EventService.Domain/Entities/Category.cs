namespace EventService.Domain.Entities;

/// <summary>
/// Entity that represents an event category.
/// </summary>
public class Category
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Required by EF Core to materialize entities; not for application code.
    private Category()
    {
        Name = null!;
    }

    private Category(Guid id, string name, string? description, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        Description = description;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory that creates a valid category.
    /// </summary>
    public static Category Create(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var now = DateTimeOffset.UtcNow;

        return new Category(
            id: Guid.NewGuid(),
            name: name.Trim(),
            description: description?.Trim(),
            createdAt: now,
            updatedAt: now);
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
