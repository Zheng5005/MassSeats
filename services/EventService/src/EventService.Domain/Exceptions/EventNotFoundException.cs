namespace EventService.Domain.Exceptions;

public sealed class EventNotFoundException : DomainException
{
    public EventNotFoundException(Guid id)
        : base($"Event with id '{id}' was not found.") { }
}
