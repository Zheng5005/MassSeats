namespace UserService.Domain.Entities;

/// <summary>
/// Aggregate root that represents a user of the platform.
/// State changes go through behavior methods so the entity always
/// protects its own invariants (no anemic model).
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string? NationalId { get; private set; }
    public string? ProfileImage { get; private set; }
    public string? Phone { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Required by EF Core to materialize entities; not for application code.
    private User()
    {
        FirstName = null!;
        Email = null!;
        PasswordHash = null!;
    }

    private User(
        Guid id,
        string firstName,
        string? lastName,
        string email,
        string passwordHash,
        string? nationalId,
        string? profileImage,
        string? phone,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PasswordHash = passwordHash;
        NationalId = nationalId;
        ProfileImage = profileImage;
        Phone = phone;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// Factory that creates a valid user. The password must be hashed
    /// before reaching the domain — the domain never sees plaintext.
    /// </summary>
    public static User Create(
        string firstName,
        string? lastName,
        string email,
        string passwordHash,
        string? nationalId = null,
        string? profileImage = null,
        string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        var now = DateTimeOffset.UtcNow;

        return new User(
            id: Guid.NewGuid(),
            firstName: firstName.Trim(),
            lastName: lastName?.Trim(),
            email: email.Trim().ToLowerInvariant(),
            passwordHash: passwordHash,
            nationalId: nationalId?.Trim(),
            profileImage: profileImage?.Trim(),
            phone: phone?.Trim(),
            createdAt: now,
            updatedAt: now);
    }

    public void UpdateProfile(string firstName, string? lastName, string? phone, string? profileImage)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        FirstName = firstName.Trim();
        LastName = lastName?.Trim();
        Phone = phone?.Trim();
        ProfileImage = profileImage?.Trim();
        Touch();
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required.", nameof(newPasswordHash));

        PasswordHash = newPasswordHash;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
