namespace EventService.Domain.Exceptions;

public sealed class DuplicateVenueException : DomainException
{
    public DuplicateVenueException(string name)
        : base($"A venue with name '{name}' already exists.") { }
}
