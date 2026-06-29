namespace EventService.Domain.Exceptions;

/// <summary>
/// Thrown when an entity invariant is violated (invalid or missing data).
/// The API layer maps this to a 400 Bad Request.
/// </summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message) : base(message) { }
}
