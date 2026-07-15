using BuildingBlocks.Domain;

namespace UserService.Domain.Exceptions;

/// <summary>
/// Base type for all domain rule violations. The API layer maps these
/// to the appropriate HTTP status codes.
/// </summary>
public abstract class DomainException : BuildingBlocks.Domain.DomainException
{
    protected DomainException(string message) : base(message) { }
}
