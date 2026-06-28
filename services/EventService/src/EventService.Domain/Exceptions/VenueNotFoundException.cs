namespace EventService.Domain.Exceptions;

public sealed class VenueNotFoundException : DomainException
{
    public VenueNotFoundException(Guid id)
        : base($"Venue with id '{id}' was not found.") { }
}
