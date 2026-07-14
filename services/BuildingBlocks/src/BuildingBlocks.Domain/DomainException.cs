namespace BuildingBlocks.Domain;

/// <summary>
/// Base type for all domain rule violations across services. Each
/// service's API layer maps these to the appropriate HTTP status codes.
/// Service-specific exceptions should inherit from this.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
